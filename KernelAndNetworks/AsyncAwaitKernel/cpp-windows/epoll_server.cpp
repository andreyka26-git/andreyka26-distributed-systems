// epoll_server.cpp  -  Windows only. Straight WSAPoll, no portability layer.
//
// The same echo server as blocking_server.cpp, but ONE thread for every connection,
// using non-blocking sockets + a readiness notifier. This is
// socket-non-blocking-kernel.excalidraw.png.
//
// The file keeps the name epoll_server because that is what the diagram and the
// walkthrough call it - but epoll is Linux's. Windows' readiness API is WSAPoll,
// and the difference is worth being honest about:
//
//   epoll   registration happens ONCE (epoll_ctl ADD). The kernel hangs a callback off
//           each socket's wait queue and a wakeup pushes the ready fd onto a ready
//           list. epoll_wait then costs O(ready).
//   WSAPoll is poll(): there is no persistent registration. The WHOLE array is copied
//           into the kernel and scanned on EVERY call, so it costs O(watched).
//
// So this file has the right SHAPE - one thread, no thread per connection, sleeps in
// exactly one place, non-blocking sockets that return WSAEWOULDBLOCK instead of parking
// the thread - and it makes the same point against blocking_server.cpp. What it does
// not have is epoll's scaling curve. Windows' real answer to that is IOCP, which is
// completion-based (you hand the kernel a buffer and it tells you when the copy is
// DONE) rather than readiness-based, so it does not fit this side-by-side at all.
//
// LOOP:
//   1. recv(s) -> WSAEWOULDBLOCK. You never sleep inside recv() here.
//   2. WSAPoll(): nothing ready -> this ONE thread sleeps, no matter how many
//      connections are idle.
//   3. packet on s: NIC DPC -> tcpip.sys buffers it -> afd.sys marks the endpoint
//      readable and releases the polling thread.
//   4. WSAPoll returns; revents on each entry says what is ready NOW.
//   5. This is level-triggered and has no alternative: WSAPoll has no edge mode
//      (epoll's EPOLLET / kqueue's EV_CLEAR). Anything still ready is simply reported
//      again on the next call.
//   6. recv(s) until WSAEWOULDBLOCK, back to WSAPoll.
//
// Every log line below is tagged with the OS thread id, and there is only ever one of
// them: the same tid accepts, reads, echoes and closes for every connection. Run
// demo_clients.py against this server and against blocking_server and diff the tids. A
// client that goes silent parks nothing here - it is one entry in an array that never
// comes back ready - so the heartbeat client keeps being served, on the beat.
//
// Observe it while it runs:
//   (Get-Process epoll_server).Threads.Count   -> flat, whatever the connection count

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601  // Windows 7: inet_ntop, WSAPoll
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#pragma comment(lib, "ws2_32.lib")

#include <chrono>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

namespace {

// Both servers listen on the SAME port: you run one at a time, and
// demo_clients.py always dials the same place.
constexpr int kPort = 9000;
constexpr int kBacklog = 128;
constexpr size_t kBufSize = 4096;

// ---------------------------------------------------------------- logging

double uptime_seconds() {
    static const std::chrono::steady_clock::time_point t0 = std::chrono::steady_clock::now();
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - t0).count();
}

unsigned long tid() { return GetCurrentThreadId(); }

// One printf per line, tagged with the tid. Here it is always the SAME tid - that is
// the point of the file.
void logf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    printf("[%7.2fs][tid %-6lu] %s\n", uptime_seconds(), tid(), msg);
}

void elogf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    fprintf(stderr, "[%7.2fs][tid %-6lu] %s\n", uptime_seconds(), tid(), msg);
}

// Winsock does not use errno: every failure is WSAGetLastError().
std::string wsa_error(int e) {
    char* buf = nullptr;
    DWORD n = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                                 FORMAT_MESSAGE_IGNORE_INSERTS,
                             nullptr, (DWORD)e, 0, (char*)&buf, 0, nullptr);
    std::string out = (n && buf) ? std::string(buf, n) : ("error " + std::to_string(e));
    if (buf) LocalFree(buf);
    while (!out.empty() && (out.back() == '\n' || out.back() == '\r')) out.pop_back();
    return out;
}

[[noreturn]] void die(const char* what) {
    fprintf(stderr, "%s: %s\n", what, wsa_error(WSAGetLastError()).c_str());
    exit(1);
}

// ---------------------------------------------------------------- sockets

// FIONBIO is Winsock's O_NONBLOCK: recv()/accept() stop waiting on the endpoint and
// return WSAEWOULDBLOCK the moment there is nothing to hand back.
void set_nonblocking(SOCKET s) {
    u_long on = 1;
    if (ioctlsocket(s, FIONBIO, &on) != 0) die("ioctlsocket FIONBIO");
}

SOCKET make_listener(int port, int backlog) {
    SOCKET s = socket(AF_INET, SOCK_STREAM, 0);
    if (s == INVALID_SOCKET) die("socket");

    // Deliberately no SO_REUSEADDR: on Windows it means "steal a live socket".

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons((unsigned short)port);

    if (bind(s, (sockaddr*)&addr, sizeof(addr)) != 0) {
        fprintf(stderr, "bind: %s\n", wsa_error(WSAGetLastError()).c_str());
        fprintf(stderr, "port %d is busy - is the other server still running?\n", port);
        exit(1);
    }
    if (listen(s, backlog) != 0) die("listen");
    return s;
}

std::string peer_name(const sockaddr_in& peer) {
    char ip[INET_ADDRSTRLEN] = {0};
    inet_ntop(AF_INET, (const void*)&peer.sin_addr, ip, sizeof(ip));
    return std::string(ip) + ":" + std::to_string(ntohs(peer.sin_port));
}

// ---------------------------------------------------------------- state

// Per-connection state. A blocking server keeps this implicitly, on the thread's stack
// and in its instruction pointer; with one thread for everything it has to become an
// explicit object. That trade - stack frame replaced by a heap struct plus a state
// machine - is the whole ergonomic cost of this model, and it is exactly what
// async/await hides.
struct Conn {
    std::string out;           // echoed bytes not yet accepted by the send buffer
    bool peer_closed = false;  // we saw FIN; still owe whatever is left in `out`
};

// The array handed to WSAPoll. Unlike epoll's rb-tree this lives in USER space and is
// copied into the kernel on every single call - the O(watched) cost noted at the top.
std::vector<WSAPOLLFD> g_fds;
std::unordered_map<SOCKET, Conn> g_conns;

void poll_add(SOCKET s, SHORT events) {
    WSAPOLLFD p{};
    p.fd = s;
    p.events = events;
    g_fds.push_back(p);
}

void poll_mod(SOCKET s, SHORT events) {
    for (auto& p : g_fds) {
        if (p.fd == s) {
            p.events = events;
            return;
        }
    }
    poll_add(s, events);
}

void poll_del(SOCKET s) {
    for (size_t i = 0; i < g_fds.size(); ++i) {
        if (g_fds[i].fd == s) {
            g_fds.erase(g_fds.begin() + (ptrdiff_t)i);
            return;
        }
    }
}

// Rearm: ask only for what we currently care about. POLLWRNORM is requested only while
// something is buffered (a writable socket is almost always ready, so a permanent write
// interest would wake us on every loop). POLLRDNORM is dropped once the peer sent FIN -
// EOF reads as "readable" forever, and WSAPoll is level-triggered, so the loop would
// spin on it.
void rearm(SOCKET s, const Conn& c) {
    SHORT events = 0;
    if (!c.peer_closed) events |= POLLRDNORM;
    if (!c.out.empty()) events |= POLLWRNORM;
    poll_mod(s, events);
}

void close_conn(SOCKET s) {
    poll_del(s);
    g_conns.erase(s);
    closesocket(s);
    logf("conn fd=%lld: closed (live=%zu)", (long long)s, g_conns.size());
}

// Try to flush what we owe. Returns false when the connection is finished (error, or
// peer half-closed and we have handed back everything).
bool flush_pending(SOCKET s) {
    Conn& c = g_conns[s];
    while (!c.out.empty()) {
        int w = send(s, c.out.data(), (int)c.out.size(), 0);
        if (w > 0) {
            c.out.erase(0, (size_t)w);
            continue;
        }
        int e = WSAGetLastError();
        if (w == SOCKET_ERROR && e == WSAEINTR) continue;
        if (w == SOCKET_ERROR && e == WSAEWOULDBLOCK) {
            // Send buffer full. A blocking send() would wait on this same endpoint
            // until ACKs freed space; instead we keep the tail and ask WSAPoll to tell
            // us when there is room.
            rearm(s, c);
            return true;
        }
        elogf("conn fd=%lld: send: %s", (long long)s, wsa_error(e).c_str());
        return false;
    }
    // Everything is out. If the peer already sent FIN there is nothing left to do.
    if (c.peer_closed) return false;
    rearm(s, c);
    return true;
}

// Drain the socket. Level-triggered, so this loop is only an optimisation (fewer
// WSAPoll round trips) - anything left behind would be reported again next call.
bool drain_and_echo(SOCKET s) {
    Conn& c = g_conns[s];
    char buf[kBufSize];
    for (;;) {
        int n = recv(s, buf, (int)sizeof(buf), 0);
        if (n > 0) {
            logf("conn fd=%lld: read %d bytes, echoing them back", (long long)s, n);
            c.out.append(buf, (size_t)n);
            continue;
        }
        if (n == 0) {                                     // FIN. Note we must still
            logf("conn fd=%lld: peer closed", (long long)s);  // flush what we owe before
            c.peer_closed = true;   // closing - the blocking server got this for free by
            break;                  // writing before it ever saw EOF.
        }
        int e = WSAGetLastError();
        if (e == WSAEINTR) continue;
        if (e == WSAEWOULDBLOCK) {
            // *** The whole point: socket drained, and we did NOT sleep in recv(). ***
            // A silent client costs this thread one WSAEWOULDBLOCK, not a parked thread.
            break;
        }
        elogf("conn fd=%lld: recv: %s", (long long)s, wsa_error(e).c_str());
        return false;
    }
    return flush_pending(s);
}

void accept_all(SOCKET listen_s) {
    for (;;) {
        sockaddr_in peer{};
        int peer_len = sizeof(peer);
        SOCKET s = accept(listen_s, (sockaddr*)&peer, &peer_len);
        if (s == INVALID_SOCKET) {
            int e = WSAGetLastError();
            if (e == WSAEINTR) continue;
            if (e == WSAEWOULDBLOCK) return;  // backlog drained
            elogf("accept: %s", wsa_error(e).c_str());
            return;
        }
        set_nonblocking(s);
        // POLLRDNORM here means "data to read, or EOF"; on the listening socket it
        // means "accept would succeed".
        poll_add(s, POLLRDNORM);
        g_conns[s];  // materialise the state object for this connection

        logf("conn fd=%lld %s: open, no thread was created for it (live=%zu)",
             (long long)s, peer_name(peer).c_str(), g_conns.size());
    }
}

}  // namespace

int main() {
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        fprintf(stderr, "WSAStartup failed\n");
        return 1;
    }
    setvbuf(stdout, nullptr, _IONBF, 0);

    SOCKET listen_s = make_listener(kPort, kBacklog);
    set_nonblocking(listen_s);
    poll_add(listen_s, POLLRDNORM);

    logf("WSAPoll echo server on port %d (pid=%lu), level-triggered", kPort,
         GetCurrentProcessId());
    logf("single thread; idle connections cost an array entry, not a thread - every "
         "line below carries the same tid");

    std::vector<std::pair<SOCKET, SHORT>> ready;
    for (;;) {
        for (auto& p : g_fds) p.revents = 0;

        // *** THE ONLY PLACE THIS PROCESS EVER SLEEPS ***
        // Everything else in this file returns WSAEWOULDBLOCK rather than waiting.
        logf("WSAPoll wait: sleeping, watching %zu connection(s) + the listener",
             g_conns.size());
        int n = WSAPoll(g_fds.data(), (ULONG)g_fds.size(), -1);
        if (n == SOCKET_ERROR) {
            if (WSAGetLastError() == WSAEINTR) continue;
            die("WSAPoll");
        }

        // Snapshot what is ready BEFORE touching anything: handling one connection can
        // close another and reshuffle g_fds underneath us.
        ready.clear();
        for (const auto& p : g_fds)
            if (p.revents) ready.push_back({p.fd, p.revents});
        logf("WSAPoll wait: woke with %d ready fd(s)", (int)ready.size());

        for (const auto& r : ready) {
            SOCKET s = r.first;
            SHORT m = r.second;

            if (s == listen_s) {
                accept_all(listen_s);
                continue;
            }
            // The socket may already have been closed earlier in this same batch.
            if (g_conns.find(s) == g_conns.end()) continue;

            if (m & (POLLERR | POLLNVAL)) {
                close_conn(s);
                continue;
            }
            if ((m & POLLWRNORM) && !flush_pending(s)) {
                close_conn(s);
                continue;
            }
            // POLLHUP is routed through the read path on purpose, so we drain and flush
            // what we owe before letting go.
            if ((m & (POLLRDNORM | POLLRDBAND | POLLHUP)) && !drain_and_echo(s)) {
                close_conn(s);
                continue;
            }
        }
    }
}
