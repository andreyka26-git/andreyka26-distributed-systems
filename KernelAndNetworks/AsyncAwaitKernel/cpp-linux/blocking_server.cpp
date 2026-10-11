// blocking_server.cpp  -  Linux only. No portability layer: these are the real syscalls.
//
// Echo server built on a BLOCKING read(). One thread per connection.
//
//   ./blocking_server                 one thread per connection (the classic design)
//   ./blocking_server --single-thread ONE thread for everything - the drawback, live
//
// This is the code path drawn in socket-blocking-kernel.excalidraw.png:
//
//   read(sk) with empty sk_receive_queue and O_NONBLOCK unset (timeo != 0)
//     -> kernel puts wait-entry {func = try_to_wake_up, private = this task} on sk->sk_wq
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
// that it is a WHOLE TASK, and that it can only ever be in one place at a time:
//
//   --single-thread: watch a silent client park the only thread inside read(). The
//       heartbeat client's connection completes its handshake (the kernel holds it in
//       the accept queue) and then just sits there: no accept(), no echo, nothing,
//       until the silent client finally sends FIN. Run demo_clients.py and read the
//       timestamps - that stall is the drawback, and it is why the default spawns.
//
//   default: correct again, at one task_struct (8KB kernel stack + scheduler
//       bookkeeping + default 8MB user stack VMA) per idle connection. Every
//       connection logs a DIFFERENT tid. That per-connection thread is what epoll
//       removes; see epoll_server.cpp, where every line logs the same tid.
//
// Observe it while it runs:
//   ps -L -o tid,stat,wchan:20,pcpu -p $(pgrep -x blocking_server)
//   cat /proc/<pid>/task/<tid>/wchan     -> sk_wait_data / inet_csk_accept

#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <unistd.h>

#include <atomic>
#include <cerrno>
#include <chrono>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>

namespace {

// Both servers listen on the SAME port: you run one at a time, and
// demo_clients.py always dials the same place.
constexpr int kPort = 9000;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;

std::atomic<int> g_live_connections{0};

// ---------------------------------------------------------------- logging

// Seconds since the first log line. The demo client's rhythm (a heartbeat every 5s)
// only means something if you can see WHEN the server reacted - or didn't.
double uptime_seconds() {
    static const std::chrono::steady_clock::time_point t0 = std::chrono::steady_clock::now();
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - t0).count();
}

// The kernel thread id - what /proc/<pid>/task/<tid>/ is keyed by. glibc only exposes
// gettid() from 2.30 onward, so go straight to the syscall.
long tid() { return (long)syscall(SYS_gettid); }

// Every line carries the tid, because the whole comparison is "which thread is doing
// what": this server hands each connection its own tid, epoll_server does everything on
// one. Formatting into a single buffer and emitting it with ONE printf keeps lines from
// interleaving when several threads log at once.
void logf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    printf("[%7.2fs][tid %-6ld] %s\n", uptime_seconds(), tid(), msg);
}

void elogf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    fprintf(stderr, "[%7.2fs][tid %-6ld] %s\n", uptime_seconds(), tid(), msg);
}

[[noreturn]] void die(const char* what) {
    fprintf(stderr, "%s: %s\n", what, strerror(errno));
    exit(1);
}

// ---------------------------------------------------------------- listener

int make_listener(int port, int backlog) {
    int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) die("socket");

    // Lets the port be reused while old connections linger in TIME_WAIT.
    int one = 1;
    setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons((unsigned short)port);

    if (bind(fd, (sockaddr*)&addr, sizeof(addr)) != 0) {
        fprintf(stderr, "bind: %s\n", strerror(errno));
        fprintf(stderr, "port %d is busy - is the other server still running?\n", port);
        exit(1);
    }
    if (listen(fd, backlog) != 0) die("listen");
    return fd;
}

std::string peer_name(const sockaddr_in& peer) {
    char ip[INET_ADDRSTRLEN] = {0};
    inet_ntop(AF_INET, &peer.sin_addr, ip, sizeof(ip));
    return std::string(ip) + ":" + std::to_string(ntohs(peer.sin_port));
}

// ---------------------------------------------------------------- connection

// Runs on its own thread (or, with --single-thread, on the accept thread itself).
// Everything here is synchronous: the thread is either running on a CPU or sleeping in
// the socket's wait queue, never spinning.
void handle_connection(int fd, std::string peer) {
    int n_live = ++g_live_connections;
    logf("conn fd=%d %s: open, this thread now owns it (live=%d)", fd, peer.c_str(),
         n_live);

    char buf[kBufSize];
    for (;;) {
        // *** THE BLOCKING CALL ***
        // If sk_receive_queue is empty this thread is descheduled here:
        //   cat /proc/<pid>/task/<tid>/stat   -> state field is 'S' (interruptible sleep)
        //   cat /proc/<pid>/task/<tid>/wchan  -> sk_wait_data
        logf("conn fd=%d: parked in recv() - this thread is now asleep on this socket's "
             "wait queue and can do NOTHING else",
             fd);
        ssize_t n = recv(fd, buf, sizeof(buf), 0);

        if (n == 0) {  // peer sent FIN
            logf("conn fd=%d: peer closed", fd);
            break;
        }
        if (n < 0) {
            if (errno == EINTR) continue;  // signal, not an error
            elogf("conn fd=%d: recv: %s", fd, strerror(errno));
            break;
        }

        logf("conn fd=%d: woke up with %zd bytes, echoing them back", fd, n);

        // send() can block too (when the send buffer is full it sleeps on the same
        // socket's wait queue, woken by ACKs freeing space). Loop over short writes.
        // MSG_NOSIGNAL: writing to a socket whose peer is gone raises SIGPIPE and would
        // kill the process.
        ssize_t off = 0;
        bool failed = false;
        while (off < n) {
            ssize_t w = send(fd, buf + off, (size_t)(n - off), MSG_NOSIGNAL);
            if (w < 0) {
                if (errno == EINTR) continue;
                elogf("conn fd=%d: send: %s", fd, strerror(errno));
                failed = true;
                break;
            }
            off += w;
        }
        if (failed) break;
    }

    close(fd);
    n_live = --g_live_connections;
    logf("conn fd=%d: closed (live=%d)", fd, n_live);
}

}  // namespace

int main(int argc, char** argv) {
    bool single_thread = false;
    for (int i = 1; i < argc; ++i) {
        if (strcmp(argv[i], "--single-thread") == 0) {
            single_thread = true;
        } else {
            fprintf(stderr, "usage: %s [--single-thread]\n", argv[0]);
            return 2;
        }
    }

    setvbuf(stdout, nullptr, _IONBF, 0);  // the log is the output; don't let it sit in a pipe

    int listen_fd = make_listener(kPort, kBacklog);

    logf("blocking echo server on port %d (pid=%d), mode=%s", kPort, (int)getpid(),
         single_thread ? "SINGLE THREAD (the drawback)" : "thread per connection");
    logf("every line below is tagged with the kernel thread id - watch how many "
         "distinct ones show up");

    for (;;) {
        sockaddr_in peer{};
        socklen_t peer_len = sizeof(peer);

        // accept() blocks exactly like read() does: the listening socket has its own
        // wait queue, and the wakeup comes from the softirq that completes the
        // three-way handshake and pushes the new sock onto the accept queue.
        logf("parked in accept() - waiting for a new connection");
        int fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd < 0) {
            if (errno == EINTR) continue;
            die("accept");
        }

        if (single_thread) {
            // No new thread: this call does not return until the client goes away, so
            // the loop cannot come back round to accept(). Connections already through
            // the handshake wait in the kernel's accept queue, invisible and unserved.
            logf("conn fd=%d: handling INLINE - no other connection can be accepted or "
                 "served until this one ends",
                 fd);
            handle_connection(fd, peer_name(peer));
        } else {
            // clone(). A whole task_struct per connection - the line that does not scale.
            std::thread(handle_connection, fd, peer_name(peer)).detach();
        }
    }
}
