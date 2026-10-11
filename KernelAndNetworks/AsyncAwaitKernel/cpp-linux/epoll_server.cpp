// epoll_server.cpp  -  Linux only. Raw epoll_create1/epoll_ctl/epoll_wait, no wrapper.
//
// The same echo server as blocking_server.cpp, but ONE thread for every connection,
// using non-blocking sockets + epoll. This is socket-non-blocking-kernel.excalidraw.png.
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
//      again next call. Edge-triggered (EPOLLET): not re-added - so you MUST drain to
//      EAGAIN or the event is lost. Build with -DEDGE_TRIGGERED=ON to compare.
//   6. read(sk) until EAGAIN, back to epoll_wait.
//
// Every log line below is tagged with the kernel thread id, and there is only ever one
// of them: the same tid accepts, reads, echoes and closes for every connection. Run
// demo_clients.py against this server and against blocking_server and diff the tids. A
// client that goes silent parks nothing here - it is just an epitem that stops turning
// up on rdllist - so the heartbeat client keeps being served, on the beat.
//
// Observe it while it runs:
//   ls /proc/$(pgrep -x epoll_server)/task | wc -l   -> 1, whatever the connection count
//   cat /proc/<pid>/task/<tid>/wchan                 -> ep_poll

#include <arpa/inet.h>
#include <fcntl.h>
#include <netinet/in.h>
#include <sys/epoll.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <unistd.h>

#include <cerrno>
#include <chrono>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unordered_map>

namespace {

// Both servers listen on the SAME port: you run one at a time, and
// demo_clients.py always dials the same place.
constexpr int kPort = 9000;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;
constexpr int kMaxEvents = 64;

#ifdef EDGE_TRIGGERED
constexpr uint32_t kTriggerFlag = EPOLLET;
constexpr const char* kTriggerName = "edge-triggered";
#else
constexpr uint32_t kTriggerFlag = 0;
constexpr const char* kTriggerName = "level-triggered";
#endif

// ---------------------------------------------------------------- logging

double uptime_seconds() {
    static const std::chrono::steady_clock::time_point t0 = std::chrono::steady_clock::now();
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - t0).count();
}

// glibc only exposes gettid() from 2.30 onward, so go straight to the syscall.
long tid() { return (long)syscall(SYS_gettid); }

// One printf per line, tagged with the tid. Here it is always the SAME tid - that is
// the point of the file.
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

// ---------------------------------------------------------------- sockets

// O_NONBLOCK makes read()/accept() take the "timeo == 0" branch: they return EAGAIN
// instead of installing a wait-entry and calling schedule(). Same kernel code path as
// the blocking case, one branch different.
void set_nonblocking(int fd) {
    int f = fcntl(fd, F_GETFL, 0);
    if (f < 0) die("fcntl F_GETFL");
    if (fcntl(fd, F_SETFL, f | O_NONBLOCK) < 0) die("fcntl F_SETFL");
}

int make_listener(int port, int backlog) {
    int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) die("socket");

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

// ---------------------------------------------------------------- state

// Per-connection state. A blocking server keeps this implicitly, on the thread's stack
// and in its instruction pointer; with one thread for everything it has to become an
// explicit object. That trade - stack frame replaced by a heap struct plus a state
// machine - is the whole ergonomic cost of the epoll model, and it is exactly what
// async/await hides.
struct Conn {
    std::string out;           // echoed bytes not yet accepted by the send buffer
    bool peer_closed = false;  // we saw FIN; still owe whatever is left in `out`
};

int g_ep = -1;  // the epoll instance: struct eventpoll { rbr, rdllist, wq }
std::unordered_map<int, Conn> g_conns;

// EPOLL_CTL_ADD is the call that walks to the socket and installs ep_poll_callback on
// its sk_wq, via the socket's poll() + a poll_table.
void ep_ctl(int op, int fd, uint32_t events) {
    epoll_event ev{};
    ev.events = events;
    ev.data.fd = fd;
    if (epoll_ctl(g_ep, op, fd, &ev) < 0) die("epoll_ctl");
}

// Rearm: ask only for what we currently care about. EPOLLOUT is requested only while
// something is buffered (a writable socket is almost always ready, so a permanent write
// interest would wake us on every loop). EPOLLIN is dropped once the peer sent FIN -
// EOF reads as "readable" forever, so a level-triggered loop would spin on it.
void rearm(int fd, const Conn& c) {
    uint32_t events = kTriggerFlag;
    if (!c.peer_closed) events |= EPOLLIN | EPOLLRDHUP;
    if (!c.out.empty()) events |= EPOLLOUT;
    ep_ctl(EPOLL_CTL_MOD, fd, events);
}

void close_conn(int fd) {
    epoll_ctl(g_ep, EPOLL_CTL_DEL, fd, nullptr);  // removes the epitem from rbr and the
    g_conns.erase(fd);                            // entry from sk_wq
    close(fd);
    logf("conn fd=%d: closed (live=%zu)", fd, g_conns.size());
}

// Try to flush what we owe. Returns false when the connection is finished (error, or
// peer half-closed and we have handed back everything).
bool flush_pending(int fd) {
    Conn& c = g_conns[fd];
    while (!c.out.empty()) {
        ssize_t w = send(fd, c.out.data(), c.out.size(), MSG_NOSIGNAL);
        if (w > 0) {
            c.out.erase(0, (size_t)w);
            continue;
        }
        if (w < 0 && errno == EINTR) continue;
        if (w < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) {
            // Send buffer full. A blocking send() would sleep on this same sk_wq until
            // ACKs freed space; instead we keep the tail and ask epoll to tell us.
            rearm(fd, c);
            return true;
        }
        elogf("conn fd=%d: send: %s", fd, strerror(errno));
        return false;
    }
    // Everything is out. If the peer already sent FIN there is nothing left to do.
    if (c.peer_closed) return false;
    rearm(fd, c);
    return true;
}

// Drain the socket. Under EPOLLET this loop to EAGAIN is mandatory: the epitem is not
// re-added to rdllist, so bytes left behind produce no further wakeup. Level-triggered
// it is merely an optimisation (fewer epoll_wait round trips).
bool drain_and_echo(int fd) {
    Conn& c = g_conns[fd];
    char buf[kBufSize];
    for (;;) {
        ssize_t n = recv(fd, buf, sizeof(buf), 0);
        if (n > 0) {
            logf("conn fd=%d: read %zd bytes, echoing them back", fd, n);
            c.out.append(buf, (size_t)n);
            continue;
        }
        if (n == 0) {                       // FIN. Note we must still flush what we owe
            logf("conn fd=%d: peer closed", fd);  // before closing - the blocking server
            c.peer_closed = true;           // got this for free by writing before EOF.
            break;
        }
        if (errno == EINTR) continue;
        if (errno == EAGAIN || errno == EWOULDBLOCK) {
            // *** The whole point: socket drained, and we did NOT sleep in read(). ***
            // A silent client costs this thread one EAGAIN, not a parked thread.
            break;
        }
        elogf("conn fd=%d: recv: %s", fd, strerror(errno));
        return false;
    }
    return flush_pending(fd);
}

void accept_all(int listen_fd) {
    for (;;) {
        sockaddr_in peer{};
        socklen_t peer_len = sizeof(peer);
        int fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd < 0) {
            if (errno == EINTR) continue;
            if (errno == EAGAIN || errno == EWOULDBLOCK) return;  // backlog drained
            elogf("accept: %s", strerror(errno));
            return;
        }
        set_nonblocking(fd);
        // EPOLLIN here means "data to read, or EOF"; on the listening socket it means
        // "accept would succeed". EPOLLRDHUP is the peer's half-close.
        ep_ctl(EPOLL_CTL_ADD, fd, kTriggerFlag | EPOLLIN | EPOLLRDHUP);
        g_conns[fd];  // materialise the state object for this connection

        logf("conn fd=%d %s: open, no thread was created for it (live=%zu)", fd,
             peer_name(peer).c_str(), g_conns.size());
    }
}

}  // namespace

int main() {
    setvbuf(stdout, nullptr, _IONBF, 0);

    // Allocates struct eventpoll: the rb-tree of watched fds (rbr), the ready list
    // (rdllist), and the wait queue this thread will sleep on (ep->wq).
    g_ep = epoll_create1(0);
    if (g_ep < 0) die("epoll_create1");

    int listen_fd = make_listener(kPort, kBacklog);
    set_nonblocking(listen_fd);
    ep_ctl(EPOLL_CTL_ADD, listen_fd, kTriggerFlag | EPOLLIN);

    logf("epoll echo server on port %d (pid=%d), %s", kPort, (int)getpid(), kTriggerName);
    logf("single thread; idle connections cost an epitem, not a task - every line below "
         "carries the same tid");

    epoll_event evs[kMaxEvents];
    for (;;) {
        // *** THE ONLY PLACE THIS PROCESS EVER SLEEPS ***
        // rdllist empty -> this thread goes on ep->wq, TASK_INTERRUPTIBLE, schedule().
        // Observe: cat /proc/<pid>/task/<tid>/wchan -> ep_poll
        logf("epoll wait: sleeping, watching %zu connection(s) + the listener",
             g_conns.size());
        int n = epoll_wait(g_ep, evs, kMaxEvents, -1);
        if (n < 0) {
            if (errno == EINTR) continue;
            die("epoll_wait");
        }
        logf("epoll wait: woke with %d ready fd(s)", n);

        // n events, each one an epitem the softirq pushed onto rdllist, re-polled by
        // epoll_wait for its live mask. No scanning of all fds - unlike select/poll,
        // the cost here is O(ready), not O(watched).
        for (int i = 0; i < n; ++i) {
            int fd = evs[i].data.fd;
            uint32_t m = evs[i].events;

            if (fd == listen_fd) {
                accept_all(listen_fd);
                continue;
            }
            // The fd may already have been closed earlier in this same batch.
            if (g_conns.find(fd) == g_conns.end()) continue;

            if (m & EPOLLERR) {
                close_conn(fd);
                continue;
            }
            if ((m & EPOLLOUT) && !flush_pending(fd)) {
                close_conn(fd);
                continue;
            }
            // EPOLLRDHUP/EPOLLHUP are routed through the read path on purpose, so we
            // drain and flush what we owe before letting go.
            if ((m & (EPOLLIN | EPOLLRDHUP | EPOLLHUP)) && !drain_and_echo(fd)) {
                close_conn(fd);
                continue;
            }
        }
    }
}
