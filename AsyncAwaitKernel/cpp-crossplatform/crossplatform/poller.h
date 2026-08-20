// poller.h
//
// One readiness-notification API over the three kernels' native mechanisms:
//
//   Linux    epoll   - epoll_create1 / epoll_ctl / epoll_wait
//   macOS    kqueue  - kqueue / kevent          (the BSD design epoll was answering)
//   Windows  WSAPoll - a scalable-ish poll()    (see the caveat at the bottom)
//
// All three answer the same question - "which of these fds can I touch without
// sleeping?" - and epoll and kqueue answer it the same way: registration is done
// ONCE, the kernel hangs a callback off each socket's wait queue, and a wakeup pushes
// the ready fd onto a ready list. Cost is O(ready), not O(watched).
//
// Read epoll_server.cpp's header for the full walk through what the kernel does.

#pragma once

#include "platform.h"

#include <vector>

#if defined(__linux__)
#include <sys/epoll.h>
#elif defined(__APPLE__) || defined(__FreeBSD__) || defined(__OpenBSD__) || \
    defined(__NetBSD__)
#include <sys/event.h>
#include <sys/time.h>
#define POLLER_KQUEUE 1
#elif defined(_WIN32)
#define POLLER_WSAPOLL 1
#else
#error "no readiness backend for this platform"
#endif

#if defined(EDGE_TRIGGERED) && defined(POLLER_WSAPOLL)
#error "EDGE_TRIGGERED needs epoll (EPOLLET) or kqueue (EV_CLEAR); WSAPoll has no edge mode"
#endif

namespace poller {

// What we ask for, and what we are told.
enum : unsigned {
    kRead = 1u << 0,    // readable, or EOF pending
    kWrite = 1u << 1,   // writable (send buffer has room)
    kHangup = 1u << 2,  // peer closed; still routed through the read path so we can
                        // drain and flush what we owe before letting go
    kError = 1u << 3,   // socket error; drop it
};

struct Event {
    plat::socket_t fd;
    unsigned mask;
};

constexpr int kMaxEvents = 64;

#ifdef EDGE_TRIGGERED
constexpr const char* kTriggerName = "edge-triggered";
#else
constexpr const char* kTriggerName = "level-triggered";
#endif

// ============================================================ Linux: epoll
#if defined(__linux__)

class Poller {
public:
    Poller() {
        // Allocates struct eventpoll: the rb-tree of watched fds (rbr), the ready
        // list (rdllist), and the wait queue this thread will sleep on (ep->wq).
        ep_ = epoll_create1(0);
        if (ep_ < 0) plat::die("epoll_create1");
    }
    ~Poller() {
        if (ep_ >= 0) ::close(ep_);
    }
    Poller(const Poller&) = delete;
    Poller& operator=(const Poller&) = delete;

    static const char* name() { return "epoll"; }

    // EPOLL_CTL_ADD is the call that walks to the socket and installs
    // ep_poll_callback on its sk_wq, via the socket's poll() + a poll_table.
    void add(plat::socket_t fd, unsigned interest) { ctl(EPOLL_CTL_ADD, fd, interest); }
    void mod(plat::socket_t fd, unsigned interest) { ctl(EPOLL_CTL_MOD, fd, interest); }
    void del(plat::socket_t fd) { epoll_ctl(ep_, EPOLL_CTL_DEL, fd, nullptr); }

    // *** THE ONLY PLACE THIS PROCESS EVER SLEEPS ***
    // rdllist empty -> this thread goes on ep->wq, TASK_INTERRUPTIBLE, schedule().
    // Observe with: cat /proc/<pid>/task/<tid>/wchan  -> ep_poll
    int wait(std::vector<Event>& out) {
        epoll_event evs[kMaxEvents];
        int n = epoll_wait(ep_, evs, kMaxEvents, -1);
        if (n < 0) {
            if (plat::interrupted(plat::last_error())) return 0;
            plat::die("epoll_wait");
        }
        out.clear();
        for (int i = 0; i < n; ++i)
            out.push_back({evs[i].data.fd, from_native(evs[i].events)});
        return (int)out.size();
    }

private:
    void ctl(int op, plat::socket_t fd, unsigned interest) {
        epoll_event ev{};
        ev.events = to_native(interest);
        ev.data.fd = fd;
        if (epoll_ctl(ep_, op, fd, &ev) < 0) plat::die("epoll_ctl");
    }

    static uint32_t to_native(unsigned interest) {
        uint32_t e = 0;
#ifdef EDGE_TRIGGERED
        // Not re-added to rdllist while still ready - so you MUST drain to EAGAIN
        // or the event is simply lost.
        e |= EPOLLET;
#endif
        if (interest & kRead) e |= EPOLLIN | EPOLLRDHUP;
        if (interest & kWrite) e |= EPOLLOUT;
        return e;
    }

    static unsigned from_native(uint32_t e) {
        unsigned m = 0;
        if (e & (EPOLLIN | EPOLLRDHUP)) m |= kRead;
        if (e & EPOLLOUT) m |= kWrite;
        if (e & EPOLLHUP) m |= kHangup;
        if (e & EPOLLERR) m |= kError;
        return m;
    }

    int ep_ = -1;
};

// ============================================================ macOS/BSD: kqueue
#elif defined(POLLER_KQUEUE)

class Poller {
public:
    Poller() {
        kq_ = kqueue();
        if (kq_ < 0) plat::die("kqueue");
    }
    ~Poller() {
        if (kq_ >= 0) ::close(kq_);
    }
    Poller(const Poller&) = delete;
    Poller& operator=(const Poller&) = delete;

    static const char* name() { return "kqueue"; }

    // kqueue has no ADD/MOD split: EV_ADD on an existing knote just updates it, so
    // registering and re-arming are the same call. Filters we do not want right now
    // stay registered but EV_DISABLE'd.
    void add(plat::socket_t fd, unsigned interest) { set_interest(fd, interest); }
    void mod(plat::socket_t fd, unsigned interest) { set_interest(fd, interest); }

    void del(plat::socket_t fd) {
        struct kevent ch[2];
        EV_SET(&ch[0], (uintptr_t)fd, EVFILT_READ, EV_DELETE, 0, 0, nullptr);
        EV_SET(&ch[1], (uintptr_t)fd, EVFILT_WRITE, EV_DELETE, 0, 0, nullptr);
        kevent(kq_, ch, 2, nullptr, 0, nullptr);  // ENOENT is fine; close() unregisters too
    }

    // The kqueue equivalent of epoll_wait: sleeps on the kq's wait queue until a
    // knote lands on its ready list. Same O(ready) shape.
    int wait(std::vector<Event>& out) {
        struct kevent evs[kMaxEvents];
        int n = kevent(kq_, nullptr, 0, evs, kMaxEvents, nullptr);
        if (n < 0) {
            if (plat::interrupted(plat::last_error())) return 0;
            plat::die("kevent wait");
        }
        // kqueue reports one event per (fd, filter); the caller wants one per fd,
        // otherwise a socket closed on its read event would be touched again on its
        // write event in the same batch.
        out.clear();
        for (int i = 0; i < n; ++i) {
            plat::socket_t fd = (plat::socket_t)evs[i].ident;
            unsigned m = 0;
            if (evs[i].flags & EV_ERROR) {
                m |= kError;
            } else if (evs[i].filter == EVFILT_READ) {
                m |= kRead;  // EV_EOF rides along here; read() returning 0 is the signal
            } else if (evs[i].filter == EVFILT_WRITE) {
                m |= kWrite;
            }

            bool merged = false;
            for (auto& e : out) {
                if (e.fd == fd) {
                    e.mask |= m;
                    merged = true;
                    break;
                }
            }
            if (!merged) out.push_back({fd, m});
        }
        return (int)out.size();
    }

private:
    void set_interest(plat::socket_t fd, unsigned interest) {
        unsigned short base = EV_ADD;
#ifdef EDGE_TRIGGERED
        base |= EV_CLEAR;  // kqueue's edge mode: same drain-to-EAGAIN obligation
#endif
        struct kevent ch[2];
        EV_SET(&ch[0], (uintptr_t)fd, EVFILT_READ,
               base | ((interest & kRead) ? EV_ENABLE : EV_DISABLE), 0, 0, nullptr);
        EV_SET(&ch[1], (uintptr_t)fd, EVFILT_WRITE,
               base | ((interest & kWrite) ? EV_ENABLE : EV_DISABLE), 0, 0, nullptr);
        if (kevent(kq_, ch, 2, nullptr, 0, nullptr) < 0) plat::die("kevent register");
    }

    int kq_ = -1;
};

// ============================================================ Windows: WSAPoll
#else

// Honest caveat: WSAPoll is poll(), not epoll. There is no persistent registration -
// the whole fd array is copied into the kernel and scanned on EVERY call, so the cost
// is O(watched), not O(ready). It has the right shape for reading this code (one
// thread, no thread per connection, sleeps in exactly one place), but the scaling
// claim epoll and kqueue make is not this API's. Windows' real answer is IOCP, which
// is completion-based - you hand the kernel a buffer and it tells you when the copy
// is DONE - rather than readiness-based, so it does not fit this side-by-side at all.
class Poller {
public:
    Poller() = default;
    Poller(const Poller&) = delete;
    Poller& operator=(const Poller&) = delete;

    static const char* name() { return "WSAPoll"; }

    void add(plat::socket_t fd, unsigned interest) {
        WSAPOLLFD p{};
        p.fd = fd;
        p.events = to_native(interest);
        fds_.push_back(p);
    }

    void mod(plat::socket_t fd, unsigned interest) {
        for (auto& p : fds_) {
            if (p.fd == fd) {
                p.events = to_native(interest);
                return;
            }
        }
        add(fd, interest);
    }

    void del(plat::socket_t fd) {
        for (size_t i = 0; i < fds_.size(); ++i) {
            if (fds_[i].fd == fd) {
                fds_.erase(fds_.begin() + (ptrdiff_t)i);
                return;
            }
        }
    }

    // The one blocking call in the process. Everything else returns WSAEWOULDBLOCK.
    int wait(std::vector<Event>& out) {
        for (auto& p : fds_) p.revents = 0;
        int n = WSAPoll(fds_.data(), (ULONG)fds_.size(), -1);
        if (n == SOCKET_ERROR) {
            if (plat::interrupted(plat::last_error())) return 0;
            plat::die("WSAPoll");
        }
        out.clear();
        for (const auto& p : fds_) {
            unsigned m = from_native(p.revents);
            if (m) out.push_back({p.fd, m});
        }
        return (int)out.size();
    }

private:
    static SHORT to_native(unsigned interest) {
        SHORT e = 0;
        if (interest & kRead) e |= POLLRDNORM;
        if (interest & kWrite) e |= POLLWRNORM;
        return e;
    }

    static unsigned from_native(SHORT revents) {
        unsigned m = 0;
        if (revents & (POLLRDNORM | POLLRDBAND)) m |= kRead;
        if (revents & POLLWRNORM) m |= kWrite;
        if (revents & POLLHUP) m |= kHangup;
        if (revents & (POLLERR | POLLNVAL)) m |= kError;
        return m;
    }

    std::vector<WSAPOLLFD> fds_;
};

#endif

}  // namespace poller
