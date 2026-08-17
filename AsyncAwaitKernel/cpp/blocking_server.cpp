// blocking_server.cpp
//
// Echo server built on BLOCKING read(). One thread per connection.
//
// This is the code path drawn in socket-blocking-kernel.excalidraw.png:
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
// The cost of this model is one kernel task (8KB kernel stack + scheduler bookkeeping
// + default 8MB user stack VMA) per idle connection. That is what epoll removes;
// see epoll_server.cpp.

#include <arpa/inet.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <unistd.h>

#include <atomic>
#include <cerrno>
#include <cstdio>
#include <cstring>
#include <string>
#include <thread>

namespace {

constexpr int kPort = 9001;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;

std::atomic<int> g_live_connections{0};

// glibc only exposes gettid() from 2.30 onward; go straight to the syscall so this
// builds on older distros too. The tid is what /proc/<pid>/task/<tid>/ is keyed by.
long thread_id() { return (long)syscall(SYS_gettid); }

void die(const char* what) {
    std::fprintf(stderr, "%s: %s\n", what, std::strerror(errno));
    std::exit(1);
}

// Runs on its own thread. Everything here is synchronous: the thread is either
// running on a CPU or sleeping in the socket's wait queue, never spinning.
void handle_connection(int fd, std::string peer) {
    int n_live = ++g_live_connections;
    std::printf("[conn fd=%d %s] open (live=%d, tid=%ld)\n", fd, peer.c_str(), n_live,
                thread_id());

    char buf[kBufSize];
    for (;;) {
        // *** THE BLOCKING CALL ***
        // If sk_receive_queue is empty this thread is descheduled here. Check with:
        //   cat /proc/<pid>/task/<tid>/stat   -> state field is 'S' (interruptible sleep)
        //   cat /proc/<pid>/task/<tid>/wchan  -> e.g. sk_wait_data / inet_csk_accept
        ssize_t n = read(fd, buf, sizeof(buf));

        if (n == 0) {  // peer sent FIN
            std::printf("[conn fd=%d] peer closed\n", fd);
            break;
        }
        if (n < 0) {
            if (errno == EINTR) continue;  // signal, not an error
            std::fprintf(stderr, "[conn fd=%d] read: %s\n", fd, std::strerror(errno));
            break;
        }

        std::printf("[conn fd=%d] woke up with %zd bytes\n", fd, n);

        // write() can block too (when the send buffer is full it sleeps on the same
        // socket's wait queue, woken by ACKs freeing space). Loop over short writes.
        ssize_t off = 0;
        while (off < n) {
            ssize_t w = write(fd, buf + off, (size_t)(n - off));
            if (w < 0) {
                if (errno == EINTR) continue;
                std::fprintf(stderr, "[conn fd=%d] write: %s\n", fd, std::strerror(errno));
                goto done;
            }
            off += w;
        }
    }
done:
    close(fd);
    n_live = --g_live_connections;
    std::printf("[conn fd=%d] closed (live=%d)\n", fd, n_live);
}

}  // namespace

int main() {
    int listen_fd = socket(AF_INET, SOCK_STREAM, 0);
    if (listen_fd < 0) die("socket");

    int one = 1;
    setsockopt(listen_fd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons(kPort);

    if (bind(listen_fd, (sockaddr*)&addr, sizeof(addr)) < 0) die("bind");
    if (listen(listen_fd, kBacklog) < 0) die("listen");

    std::printf("blocking echo server on port %d (pid=%d)\n", kPort, getpid());
    std::printf("one thread per connection; each idle thread sleeps in the socket's "
                "wait queue\n");

    for (;;) {
        sockaddr_in peer{};
        socklen_t peer_len = sizeof(peer);

        // accept() blocks exactly like read() does: the listening socket has its own
        // wait queue, and the wakeup comes from the softirq that completes the
        // three-way handshake and pushes the new sock onto the accept queue.
        int fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd < 0) {
            if (errno == EINTR) continue;
            die("accept");
        }

        char ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &peer.sin_addr, ip, sizeof(ip));
        std::string desc = std::string(ip) + ":" + std::to_string(ntohs(peer.sin_port));

        // A whole kernel task per connection. This is the line that does not scale.
        std::thread(handle_connection, fd, desc).detach();
    }
}
