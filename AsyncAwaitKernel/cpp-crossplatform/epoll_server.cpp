// epoll_server.cpp
//
// The same echo server, but ONE thread for every connection, using non-blocking
// sockets + a readiness notifier. This is socket-non-blocking-kernel.excalidraw.png.
//
// The notifier is whatever the OS provides - epoll on Linux, kqueue on macOS,
// WSAPoll on Windows - behind the small Poller in poller.h. The walkthrough below is
// epoll's, because that is what the diagram draws; kqueue is the same design with BSD
// names, and poller.h notes where WSAPoll genuinely differs.
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
//      again next call. Edge-triggered (EPOLLET / kqueue EV_CLEAR): not re-added - so
//      you MUST drain to EAGAIN or the event is lost. Build with -DEDGE_TRIGGERED=ON
//      to compare.
//   6. read(sk) until EAGAIN, back to epoll_wait.
//
// Every log line below is tagged with the OS thread id, and there is only ever one of
// them: the same tid accepts, reads, echoes and closes for every connection. Run
// demo_clients.py against this server and against blocking_server and diff the tids. A
// client that goes silent parks nothing here - it is just an epitem that stops turning
// up on rdllist - so the heartbeat client keeps being served, on the beat.

#include "crossplatform/platform.h"
#include "crossplatform/poller.h"

#include <string>
#include <unordered_map>
#include <vector>

namespace {

// Both servers listen on the SAME port: you run one at a time, and
// demo_clients.py always dials the same place.
constexpr int kPort = 9000;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;

// Per-connection state. A blocking server keeps this implicitly, on the thread's stack
// and in its instruction pointer; with one thread for everything it has to become an
// explicit object. That trade - stack frame replaced by a heap struct plus a state
// machine - is the whole ergonomic cost of the epoll model, and it is exactly what
// async/await hides.
struct Conn {
    std::string out;           // echoed bytes not yet accepted by the send buffer
    bool peer_closed = false;  // we saw FIN; still owe whatever is left in `out`
};

std::unordered_map<plat::socket_t, Conn> g_conns;

void close_conn(poller::Poller& p, plat::socket_t fd) {
    p.del(fd);  // removes the epitem from rbr and the entry from sk_wq
    g_conns.erase(fd);
    plat::close_socket(fd);
    plat::logf("conn fd=%lld: closed (live=%zu)", (long long)fd, g_conns.size());
}

// Rearm: ask only for what we currently care about. kWrite is requested only while
// something is buffered (a writable socket is almost always ready, so a permanent
// write interest would wake us on every loop). kRead is dropped once the peer sent
// FIN - EOF reads as "readable" forever, so a level-triggered loop would spin on it.
void rearm(poller::Poller& p, plat::socket_t fd, const Conn& c) {
    unsigned interest = 0;
    if (!c.peer_closed) interest |= poller::kRead;
    if (!c.out.empty()) interest |= poller::kWrite;
    p.mod(fd, interest);
}

// Try to flush what we owe. Returns false when the connection is finished (error, or
// peer half-closed and we have handed back everything).
bool flush_pending(poller::Poller& p, plat::socket_t fd) {
    Conn& c = g_conns[fd];
    while (!c.out.empty()) {
        long long w = plat::net_send(fd, c.out.data(), c.out.size());
        if (w > 0) {
            c.out.erase(0, (size_t)w);
            continue;
        }
        int e = plat::last_error();
        if (w < 0 && plat::interrupted(e)) continue;
        if (w < 0 && plat::would_block(e)) {
            // Send buffer full. A blocking send() would sleep on this same sk_wq until
            // ACKs freed space; instead we keep the tail and ask the poller to tell us.
            rearm(p, fd, c);
            return true;
        }
        plat::elogf("conn fd=%lld: send: %s", (long long)fd,
                    plat::error_string(e).c_str());
        return false;
    }
    // Everything is out. If the peer already sent FIN there is nothing left to do.
    if (c.peer_closed) return false;
    rearm(p, fd, c);
    return true;
}

// Drain the socket. Under edge-triggered this loop to EAGAIN is mandatory: the epitem
// is not re-added to rdllist, so bytes left behind produce no further wakeup. Under
// level-triggered it is merely an optimisation (fewer epoll_wait round trips).
bool drain_and_echo(poller::Poller& p, plat::socket_t fd) {
    Conn& c = g_conns[fd];
    char buf[kBufSize];
    for (;;) {
        long long n = plat::net_recv(fd, buf, sizeof(buf));
        if (n > 0) {
            plat::logf("conn fd=%lld: read %lld bytes, echoing them back",
                       (long long)fd, n);
            c.out.append(buf, (size_t)n);
            continue;
        }
        if (n == 0) {  // FIN. Note we must still flush what we owe before closing -
            plat::logf("conn fd=%lld: peer closed", (long long)fd);       // the blocking
            c.peer_closed = true;  // server got this for free by writing before EOF.
            break;
        }
        int e = plat::last_error();
        if (plat::interrupted(e)) continue;
        if (plat::would_block(e)) {
            // *** The whole point: socket drained, and we did NOT sleep in read(). ***
            // A silent client costs this thread one EAGAIN, not a parked thread.
            break;
        }
        plat::elogf("conn fd=%lld: recv: %s", (long long)fd,
                    plat::error_string(e).c_str());
        return false;
    }
    return flush_pending(p, fd);
}

void accept_all(poller::Poller& p, plat::socket_t listen_fd) {
    for (;;) {
        sockaddr_in peer{};
        plat::socklen_t_compat peer_len = sizeof(peer);
        plat::socket_t fd = accept(listen_fd, (sockaddr*)&peer, &peer_len);
        if (fd == plat::kInvalidSocket) {
            int e = plat::last_error();
            if (plat::interrupted(e)) continue;
            if (plat::would_block(e)) return;  // backlog drained
            plat::elogf("accept: %s", plat::error_string(e).c_str());
            return;
        }
        plat::set_nonblocking(fd);
        plat::suppress_sigpipe(fd);
        // kRead here means "data to read, or EOF"; on the listening socket it means
        // "accept would succeed".
        p.add(fd, poller::kRead);
        g_conns[fd];  // materialise the state object for this connection

        plat::logf("conn fd=%lld %s: open, no thread was created for it (live=%zu)",
                   (long long)fd, plat::peer_name(peer).c_str(), g_conns.size());
    }
}

}  // namespace

int main() {
    plat::NetInit net_init;  // WSAStartup on Windows, nothing anywhere else
    plat::unbuffer_stdout();

    plat::socket_t listen_fd = plat::make_listener(kPort, kBacklog);
    plat::set_nonblocking(listen_fd);

    poller::Poller p;
    p.add(listen_fd, poller::kRead);

    plat::logf("%s echo server on port %d (pid=%ld), %s", poller::Poller::name(), kPort,
               plat::process_id(), poller::kTriggerName);
    plat::logf("single thread; idle connections cost an epitem, not a task - every line "
               "below carries the same tid");

    std::vector<poller::Event> events;
    for (;;) {
        // *** THE ONLY PLACE THIS PROCESS EVER SLEEPS ***
        // rdllist empty -> this thread goes on ep->wq, TASK_INTERRUPTIBLE, schedule().
        // Observe: /proc/<pid>/task/<tid>/wchan -> ep_poll
        plat::logf("%s wait: sleeping, watching %zu connection(s) + the listener",
                   poller::Poller::name(), g_conns.size());
        int n = p.wait(events);
        plat::logf("%s wait: woke with %d ready fd(s)", poller::Poller::name(), n);

        // n events, each one an epitem the softirq pushed onto rdllist, re-polled by
        // epoll_wait for its live mask. No scanning of all fds - unlike select/poll,
        // the cost here is O(ready), not O(watched).
        for (int i = 0; i < n; ++i) {
            plat::socket_t fd = events[i].fd;
            unsigned m = events[i].mask;

            if (fd == listen_fd) {
                accept_all(p, listen_fd);
                continue;
            }
            // The fd may already have been closed earlier in this same batch.
            if (g_conns.find(fd) == g_conns.end()) continue;

            if (m & poller::kError) {
                close_conn(p, fd);
                continue;
            }
            if ((m & poller::kWrite) && !flush_pending(p, fd)) {
                close_conn(p, fd);
                continue;
            }
            if ((m & (poller::kRead | poller::kHangup)) && !drain_and_echo(p, fd)) {
                close_conn(p, fd);
                continue;
            }
        }
    }
}
