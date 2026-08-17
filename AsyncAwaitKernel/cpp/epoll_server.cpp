// epoll_server.cpp
//
// The same echo server, but ONE thread for every connection, using non-blocking
// fds + epoll. This is socket-non-blocking-kernel.excalidraw.png.
//
// SETUP - epoll_ctl(ADD, sk):
//   kernel makes an epitem, inserts it into ep->rbr (red-black tree of monitored fds),
//   then calls the socket's poll() handing it a poll_table (an envelope carrying
//   epoll's installer, because only the socket knows where its sk_wq lives).
//   poll() returns the current readiness mask AND runs the installer:
//     add_wait_queue(sk_wq, entry) with entry.func = ep_poll_callback,
//                                       entry.private -> this epitem.
//   Same sk_wq the blocking read() would have slept on - different callback.
//   N sockets = N entries, each pointing at its own epitem.
//
// LOOP:
//   1. read(sk) -> EAGAIN. You never sleep inside read() here.
//   2. epoll_wait(): rdllist empty -> wait-entry {private = this thread} onto ep->wq,
//      schedule(). ONE thread sleeps, no matter how many connections are idle.
//   3. packet on sk: NIC IRQ -> softirq -> data into sk_receive_queue ->
//      wake_up(sk_wq) -> ep_poll_callback (still in softirq context). It already
//      knows the fd via private->epitem, so nothing is scanned. It:
//        (a) appends the epitem to ep->rdllist  (fds, not data)
//        (b) wake_up(ep->wq) -> the epoll_wait thread becomes RUNNING and lands on a
//            run queue.
//   4. scheduler runs that thread; epoll_wait drains rdllist. For each epitem it
//      RE-CALLS the fd's poll() for the live mask, ANDs it with the interest mask,
//      and fills one epoll_event {fd, mask}. Re-polling (rather than trusting the
//      stored entry) is how it knows which events fired and reflects state NOW,
//      not state when the IRQ hit.
//   5. Level-triggered: still-ready epitems are re-added to rdllist and reported
//      again next call. Edge-triggered (EPOLLET): not re-added - so you MUST drain
//      to EAGAIN or the event is lost. Build with -DUSE_EPOLLET to compare.
//   6. read(sk) until EAGAIN, back to epoll_wait.

#include <arpa/inet.h>
#include <fcntl.h>
#include <netinet/in.h>
#include <sys/epoll.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <unistd.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <unordered_map>

namespace {

constexpr int kPort = 9002;
constexpr int kBacklog = 128;
constexpr int kMaxEvents = 64;
constexpr size_t kBufSize = 4096;

#ifdef USE_EPOLLET
constexpr uint32_t kTriggerMode = EPOLLET;
constexpr const char* kTriggerName = "edge-triggered (EPOLLET)";
#else
constexpr uint32_t kTriggerMode = 0;
constexpr const char* kTriggerName = "level-triggered (default)";
#endif

void die(const char* what) {
    std::fprintf(stderr, "%s: %s\n", what, std::strerror(errno));
    std::exit(1);
}

// Sets O_NONBLOCK on the fd: read()/accept() take the timeo == 0 branch and return
// -EAGAIN instead of installing a wait-entry and calling schedule(). Same kernel code
// path as the blocking case, one branch different.
void set_nonblocking(int fd) {
    int flags = fcntl(fd, F_GETFL, 0);
    if (flags < 0) die("fcntl F_GETFL");
    if (fcntl(fd, F_SETFL, flags | O_NONBLOCK) < 0) die("fcntl F_SETFL");
}

void epoll_add(int ep, int fd, uint32_t events) {
    epoll_event ev{};
    ev.events = events;
    ev.data.fd = fd;
    // This is the call that walks to the socket and installs ep_poll_callback on sk_wq.
    if (epoll_ctl(ep, EPOLL_CTL_ADD, fd, &ev) < 0) die("epoll_ctl ADD");
}

// Per-connection state. A blocking server keeps this implicitly, on the thread's stack
// and in its instruction pointer; with one thread for everything it has to become an
// explicit object. That trade - stack frame replaced by a heap struct plus a state
// machine - is the whole ergonomic cost of the epoll model, and it is exactly what
// async/await hides.
struct Conn {
    std::string out;           // echoed bytes not yet accepted by the send buffer
    bool peer_closed = false;  // we saw FIN; still owe whatever is left in `out`
};

std::unordered_map<int, Conn> g_conns;

void close_conn(int ep, int fd) {
    epoll_ctl(ep, EPOLL_CTL_DEL, fd, nullptr);  // removes epitem from rbr + sk_wq entry
    g_conns.erase(fd);
    close(fd);
    std::printf("[conn fd=%d] closed (live=%zu)\n", fd, g_conns.size());
}

// Rearm: ask only for what we currently care about. EPOLLOUT is requested only while
// something is buffered (a writable socket is almost always ready, so a permanent
// EPOLLOUT interest would wake us on every loop). EPOLLIN is dropped once the peer sent
// FIN - EOF reads as "readable" forever, so a level-triggered loop would spin on it.
void rearm(int ep, int fd, const Conn& c) {
    epoll_event ev{};
    ev.events = kTriggerMode;
    if (!c.peer_closed) ev.events |= EPOLLIN | EPOLLRDHUP;
    if (!c.out.empty()) ev.events |= EPOLLOUT;
    ev.data.fd = fd;
    if (epoll_ctl(ep, EPOLL_CTL_MOD, fd, &ev) < 0) die("epoll_ctl MOD");
}

// Try to flush what we owe. Returns false when the connection is finished (error, or
// peer half-closed and we have handed back everything).
bool flush_pending(int ep, int fd) {
    Conn& c = g_conns[fd];
    while (!c.out.empty()) {
        ssize_t w = write(fd, c.out.data(), c.out.size());
        if (w > 0) {
            c.out.erase(0, (size_t)w);
            continue;
        }
        if (w < 0 && errno == EINTR) continue;
        if (w < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            // Send buffer full. A blocking write() would sleep on this same sk_wq until
            // ACKs freed space; instead we keep the tail and ask epoll to tell us.
            rearm(ep, fd, c);
            return true;
        }
        std::fprintf(stderr, "[conn fd=%d] write: %s\n", fd, std::strerror(errno));
        return false;
    }
    // Everything is out. If the peer already sent FIN there is nothing left to do.
    if (c.peer_closed) return false;
    rearm(ep, fd, c);
    return true;
}

// Drain the socket. Under EPOLLET this loop to EAGAIN is mandatory: the epitem is not
// re-added to rdllist, so bytes left behind produce no further wakeup. Under
// level-triggered it is merely an optimisation (fewer epoll_wait round trips).
bool drain_and_echo(int ep, int fd) {
    Conn& c = g_conns[fd];
    char buf[kBufSize];
    for (;;) {
        ssize_t n = read(fd, buf, sizeof(buf));
        if (n > 0) {
            std::printf("[conn fd=%d] read %zd bytes\n", fd, n);
            c.out.append(buf, (size_t)n);
            continue;
        }
        if (n == 0) {  // FIN. Note we must still flush what we owe before closing -
            std::printf("[conn fd=%d] peer closed\n", fd);  // the blocking server got
            c.peer_closed = true;  // this for free by writing before it ever saw EOF.
            break;
        }
        if (errno == EINTR) continue;
        if (errno == EAGAIN || errno == EWOULDBLOCK) {
            // *** The whole point: socket drained, and we did NOT sleep in read(). ***
            break;
        }
        std::fprintf(stderr, "[conn fd=%d] read: %s\n", fd, std::strerror(errno));
        return false;
    }
    return flush_pending(ep, fd);
}

void accept_all(int ep, int listen_fd) {
    for (;;) {
        sockaddr_in peer{};
        socklen_t peer_len = sizeof(peer);
        int fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd < 0) {
            if (errno == EINTR) continue;
            if (errno == EAGAIN || errno == EWOULDBLOCK) return;  // backlog drained
            std::fprintf(stderr, "accept: %s\n", std::strerror(errno));
            return;
        }
        set_nonblocking(fd);
        // EPOLLIN here means "data to read, or EOF"; on the listening socket it means
        // "accept would succeed". EPOLLRDHUP catches the peer's half-close.
        epoll_add(ep, fd, EPOLLIN | EPOLLRDHUP | kTriggerMode);
        g_conns[fd];  // materialise the state object for this connection

        char ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &peer.sin_addr, ip, sizeof(ip));
        std::printf("[conn fd=%d %s:%u] open (live=%zu)\n", fd, ip,
                    (unsigned)ntohs(peer.sin_port), g_conns.size());
    }
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
    set_nonblocking(listen_fd);

    int ep = epoll_create1(0);  // allocates struct eventpoll: rbr, rdllist, ep->wq
    if (ep < 0) die("epoll_create1");
    epoll_add(ep, listen_fd, EPOLLIN | kTriggerMode);

    std::printf("epoll echo server on port %d (pid=%d, tid=%ld), %s\n", kPort, getpid(),
                (long)syscall(SYS_gettid), kTriggerName);
    std::printf("single thread; idle connections cost an epitem, not a task\n");

    epoll_event events[kMaxEvents];
    for (;;) {
        // *** THE ONLY PLACE THIS PROCESS EVER SLEEPS ***
        // rdllist empty -> this thread goes on ep->wq, TASK_INTERRUPTIBLE, schedule().
        // Observe: /proc/<pid>/task/<tid>/wchan -> ep_poll.
        int n = epoll_wait(ep, events, kMaxEvents, -1);
        if (n < 0) {
            if (errno == EINTR) continue;
            die("epoll_wait");
        }

        // n events, each one an epitem the softirq pushed onto rdllist, re-polled by
        // epoll_wait for its live mask. No scanning of all fds - unlike select/poll,
        // the cost here is O(ready), not O(watched).
        for (int i = 0; i < n; ++i) {
            int fd = events[i].data.fd;
            uint32_t m = events[i].events;

            if (fd == listen_fd) {
                accept_all(ep, listen_fd);
                continue;
            }
            if (m & (EPOLLHUP | EPOLLERR)) {
                close_conn(ep, fd);
                continue;
            }
            if ((m & EPOLLOUT) && !flush_pending(ep, fd)) {
                close_conn(ep, fd);
                continue;
            }
            if ((m & (EPOLLIN | EPOLLRDHUP)) && !drain_and_echo(ep, fd)) {
                close_conn(ep, fd);
                continue;
            }
        }
    }
}
