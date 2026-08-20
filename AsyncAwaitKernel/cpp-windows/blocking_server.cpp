// blocking_server.cpp  -  Windows only. Straight Winsock, no portability layer.
//
// Echo server built on a BLOCKING recv(). One thread per connection.
//
//   blocking_server.exe                 one thread per connection (the classic design)
//   blocking_server.exe --single-thread ONE thread for everything - the drawback, live
//
// This is the code path drawn in socket-blocking-kernel.excalidraw.png. The diagram and
// the notes name Linux's internals, because that is the kernel whose source you can
// read; Windows does the same thing behind different spellings:
//
//   recv(s) on a socket with nothing buffered
//     -> afd.sys queues an IRP on the endpoint and blocks the calling thread on a
//        kernel dispatcher object (the equivalent of parking on sk_wq)
//     -> the thread leaves the CPU entirely: KeWaitForSingleObject, state Waiting.
//        Zero CPU burned while waiting.
//
//   packet arrives
//     -> NIC DPC -> tcpip.sys appends to the receive buffer -> completes the IRP
//     -> KeSetEvent wakes exactly the thread waiting on THAT endpoint - no scan
//     -> the scheduler makes it Ready, then Running; recv() copies to userspace
//        and returns.
//
// The thread is never the problem while it is asleep - it burns no CPU. The problem is
// that it is a WHOLE THREAD, and that it can only ever be in one place at a time:
//
//   --single-thread: watch a silent client park the only thread inside recv(). The
//       heartbeat client's connection completes its handshake (Windows holds it in the
//       accept queue) and then just sits there: no accept(), no echo, nothing, until
//       the silent client finally sends FIN. Run demo_clients.py and read the
//       timestamps - that stall is the drawback, and it is why the default spawns.
//
//   default: correct again, at one thread (a kernel stack, an ETHREAD, scheduler
//       bookkeeping, and a 1MB reserved user stack) per idle connection. Every
//       connection logs a DIFFERENT tid. That per-connection thread is what the
//       readiness model removes; see epoll_server.cpp, where every line logs the
//       same tid.
//
// Observe it while it runs:
//   (Get-Process blocking_server).Threads.Count
//   Process Explorer -> Threads tab: every one of them in "Wait:UserRequest"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601  // Windows 7: inet_ntop
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#pragma comment(lib, "ws2_32.lib")

#include <atomic>
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

// The OS thread id - what Process Explorer and the debugger show.
unsigned long tid() { return GetCurrentThreadId(); }

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

// Winsock does not use errno: every failure is WSAGetLastError(), and the text has to
// come from FormatMessage.
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

// ---------------------------------------------------------------- listener

SOCKET make_listener(int port, int backlog) {
    SOCKET s = socket(AF_INET, SOCK_STREAM, 0);
    if (s == INVALID_SOCKET) die("socket");

    // Deliberately no SO_REUSEADDR: on Windows it means "steal a live socket", not
    // "reuse a port stuck in TIME_WAIT" as it does on the BSD sockets API.

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

// ---------------------------------------------------------------- connection

// Runs on its own thread (or, with --single-thread, on the accept thread itself).
// Everything here is synchronous: the thread is either running on a CPU or waiting on
// the socket's endpoint, never spinning.
void handle_connection(SOCKET s, std::string peer) {
    int n_live = ++g_live_connections;
    logf("conn fd=%lld %s: open, this thread now owns it (live=%d)", (long long)s,
         peer.c_str(), n_live);

    char buf[kBufSize];
    for (;;) {
        // *** THE BLOCKING CALL ***
        // Nothing buffered -> this thread is descheduled here until data arrives.
        logf("conn fd=%lld: parked in recv() - this thread is now asleep on this "
             "socket's endpoint and can do NOTHING else",
             (long long)s);
        int n = recv(s, buf, (int)sizeof(buf), 0);

        if (n == 0) {  // peer sent FIN
            logf("conn fd=%lld: peer closed", (long long)s);
            break;
        }
        if (n == SOCKET_ERROR) {
            int e = WSAGetLastError();
            if (e == WSAEINTR) continue;
            elogf("conn fd=%lld: recv: %s", (long long)s, wsa_error(e).c_str());
            break;
        }

        logf("conn fd=%lld: woke up with %d bytes, echoing them back", (long long)s, n);

        // send() can block too: when the send buffer is full it waits on the same
        // endpoint, released by ACKs freeing space. Loop over short writes.
        int off = 0;
        bool failed = false;
        while (off < n) {
            int w = send(s, buf + off, n - off, 0);
            if (w == SOCKET_ERROR) {
                int e = WSAGetLastError();
                if (e == WSAEINTR) continue;
                elogf("conn fd=%lld: send: %s", (long long)s, wsa_error(e).c_str());
                failed = true;
                break;
            }
            off += w;
        }
        if (failed) break;
    }

    closesocket(s);
    n_live = --g_live_connections;
    logf("conn fd=%lld: closed (live=%d)", (long long)s, n_live);
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

    WSADATA wsa;  // Winsock needs an explicit init; the BSD sockets API needs none
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        fprintf(stderr, "WSAStartup failed\n");
        return 1;
    }

    // The log is the output, and MSVC has no line buffering - unbuffered is fine at
    // this volume and keeps it from sitting in a pipe.
    setvbuf(stdout, nullptr, _IONBF, 0);

    SOCKET listen_s = make_listener(kPort, kBacklog);

    logf("blocking echo server on port %d (pid=%lu), mode=%s", kPort,
         GetCurrentProcessId(),
         single_thread ? "SINGLE THREAD (the drawback)" : "thread per connection");
    logf("every line below is tagged with the OS thread id - watch how many distinct "
         "ones show up");

    for (;;) {
        sockaddr_in peer{};
        int peer_len = sizeof(peer);

        // accept() blocks exactly like recv() does: the listening socket has its own
        // endpoint, and the wakeup comes from the DPC that completes the three-way
        // handshake and queues the new connection.
        logf("parked in accept() - waiting for a new connection");
        SOCKET s = accept(listen_s, (sockaddr*)&peer, &peer_len);
        if (s == INVALID_SOCKET) {
            if (WSAGetLastError() == WSAEINTR) continue;
            die("accept");
        }

        if (single_thread) {
            // No new thread: this call does not return until the client goes away, so
            // the loop cannot come back round to accept(). Connections already through
            // the handshake wait in the kernel's accept queue, invisible and unserved.
            logf("conn fd=%lld: handling INLINE - no other connection can be accepted "
                 "or served until this one ends",
                 (long long)s);
            handle_connection(s, peer_name(peer));
        } else {
            // A whole thread per connection. This is the line that does not scale.
            std::thread(handle_connection, s, peer_name(peer)).detach();
        }
    }
}
