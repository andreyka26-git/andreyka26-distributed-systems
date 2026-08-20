// blocking_server.cpp
//
// Echo server built on a BLOCKING read(). One thread per connection.
// Builds and runs on Linux, macOS and Windows (see crossplatform/platform.h for the
// socket shims).
//
//   ./blocking_server                 one thread per connection (the classic design)
//   ./blocking_server --single-thread ONE thread for everything - the drawback, live
//
// This is the code path drawn in socket-blocking-kernel.excalidraw.png. The names
// below are Linux's; macOS and Windows do the same thing with different spellings.
//
//   read(sk) with empty sk_receive_queue and O_NONBLOCK unset (timeo != 0)
//     -> kernel puts wait-entry {func = wake-me, private = this task} on sk->sk_wq
//     -> task state = TASK_INTERRUPTIBLE
//     -> schedule(): the task leaves the CPU entirely. Zero CPU burned while waiting.
//
//   packet arrives
//     -> NIC IRQ -> softirq -> data appended to sk_receive_queue
//     -> sk_data_ready -> wake_up(sk_wq) walks THIS socket's wait queue only (O(1),
//        no global scan: the queue lives inside the socket)
//     -> entry->func = try_to_wake_up: task state = TASK_RUNNING, task is enqueued
//        on a per-CPU run queue
//     -> some later schedule() picks it; read() resumes right after schedule(),
//        copies the bytes to userspace and returns.
//
// The thread is never the problem while it is asleep - it burns no CPU. The problem is
// that it is a WHOLE THREAD, and that it can only ever be in one place at a time:
//
//   --single-thread: watch a silent client park the only thread inside read(). The
//       heartbeat client's connection completes its handshake (the kernel holds it in
//       the accept queue) and then just sits there: no accept(), no echo, nothing,
//       until the silent client finally sends FIN. Run demo_clients.py and read the
//       timestamps - that stall is the drawback, and it is why the default spawns.
//
//   default: correct again, at one kernel task (8KB kernel stack + scheduler
//       bookkeeping + default 8MB user stack VMA) per idle connection. Every
//       connection logs a DIFFERENT tid. That per-connection thread is what epoll
//       removes; see epoll_server.cpp, where every line logs the same tid.

#include "crossplatform/platform.h"

#include <atomic>
#include <string>
#include <thread>

namespace {

constexpr int kPort = 9001;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;

std::atomic<int> g_live_connections{0};

// Runs on its own thread (or, with --single-thread, on the accept thread itself).
// Everything here is synchronous: the thread is either running on a CPU or sleeping in
// the socket's wait queue, never spinning.
void handle_connection(plat::socket_t fd, std::string peer) {
    int n_live = ++g_live_connections;
    plat::logf("conn fd=%lld %s: open, this thread now owns it (live=%d)", (long long)fd,
               peer.c_str(), n_live);

    plat::suppress_sigpipe(fd);

    char buf[kBufSize];
    for (;;) {
        // *** THE BLOCKING CALL ***
        // If sk_receive_queue is empty this thread is descheduled here. On Linux:
        //   cat /proc/<pid>/task/<tid>/stat   -> state field is 'S' (interruptible sleep)
        //   cat /proc/<pid>/task/<tid>/wchan  -> e.g. sk_wait_data / inet_csk_accept
        plat::logf("conn fd=%lld: parked in recv() - this thread is now asleep on this "
                   "socket's wait queue and can do NOTHING else",
                   (long long)fd);
        long long n = plat::net_recv(fd, buf, sizeof(buf));

        if (n == 0) {  // peer sent FIN
            plat::logf("conn fd=%lld: peer closed", (long long)fd);
            break;
        }
        if (n < 0) {
            int e = plat::last_error();
            if (plat::interrupted(e)) continue;  // signal, not an error
            plat::elogf("conn fd=%lld: recv: %s", (long long)fd,
                        plat::error_string(e).c_str());
            break;
        }

        plat::logf("conn fd=%lld: woke up with %lld bytes, echoing them back",
                   (long long)fd, n);

        // send() can block too (when the send buffer is full it sleeps on the same
        // socket's wait queue, woken by ACKs freeing space). Loop over short writes.
        long long off = 0;
        bool failed = false;
        while (off < n) {
            long long w = plat::net_send(fd, buf + off, (size_t)(n - off));
            if (w < 0) {
                int e = plat::last_error();
                if (plat::interrupted(e)) continue;
                plat::elogf("conn fd=%lld: send: %s", (long long)fd,
                            plat::error_string(e).c_str());
                failed = true;
                break;
            }
            off += w;
        }
        if (failed) break;
    }

    plat::close_socket(fd);
    n_live = --g_live_connections;
    plat::logf("conn fd=%lld: closed (live=%d)", (long long)fd, n_live);
}

}  // namespace

int main(int argc, char** argv) {
    bool single_thread = false;
    for (int i = 1; i < argc; ++i) {
        if (std::strcmp(argv[i], "--single-thread") == 0) {
            single_thread = true;
        } else {
            std::fprintf(stderr, "usage: %s [--single-thread]\n", argv[0]);
            return 2;
        }
    }

    plat::NetInit net_init;  // WSAStartup on Windows, nothing anywhere else
    plat::unbuffer_stdout();

    plat::socket_t listen_fd = plat::make_listener(kPort, kBacklog);

    plat::logf("blocking echo server on port %d (pid=%ld), mode=%s", kPort,
               plat::process_id(),
               single_thread ? "SINGLE THREAD (the drawback)" : "thread per connection");
    plat::logf("every line below is tagged with the OS thread id - watch how many "
               "distinct ones show up");

    for (;;) {
        sockaddr_in peer{};
        plat::socklen_t_compat peer_len = sizeof(peer);

        // accept() blocks exactly like read() does: the listening socket has its own
        // wait queue, and the wakeup comes from the softirq that completes the
        // three-way handshake and pushes the new sock onto the accept queue.
        plat::logf("parked in accept() - waiting for a new connection");
        plat::socket_t fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd == plat::kInvalidSocket) {
            if (plat::interrupted(plat::last_error())) continue;
            plat::die("accept");
        }

        if (single_thread) {
            // No new thread: this call does not return until the client goes away, so
            // the loop cannot come back round to accept(). Connections already through
            // the handshake wait in the kernel's accept queue, invisible and unserved.
            plat::logf("conn fd=%lld: handling INLINE - no other connection can be "
                       "accepted or served until this one ends",
                       (long long)fd);
            handle_connection(fd, plat::peer_name(peer));
        } else {
            // A whole kernel task per connection. This is the line that does not scale.
            std::thread(handle_connection, fd, plat::peer_name(peer)).detach();
        }
    }
}
