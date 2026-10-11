// platform.h
//
// The thin layer that lets the same socket code build on Linux, macOS and Windows.
// None of this is part of the lesson - it only smooths over the fact that Winsock
// spells things differently (SOCKET instead of int, closesocket() instead of close(),
// WSAGetLastError() instead of errno). The kernel mechanics the two servers
// demonstrate are the same shape everywhere; only the names change.

#pragma once

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601  // Windows 7: inet_ntop, WSAPoll
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#ifdef _MSC_VER
#pragma comment(lib, "ws2_32.lib")
#endif
#else
#include <arpa/inet.h>
#include <fcntl.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>
#include <cerrno>
#endif

#ifdef __linux__
#include <sys/syscall.h>
#endif
#ifdef __APPLE__
#include <pthread.h>
#endif

#include <chrono>
#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

namespace plat {

#ifdef _WIN32
using socket_t = SOCKET;
using socklen_t_compat = int;
constexpr socket_t kInvalidSocket = INVALID_SOCKET;
#else
using socket_t = int;
using socklen_t_compat = socklen_t;
constexpr socket_t kInvalidSocket = -1;
#endif

// ---------------------------------------------------------------- errors

inline int last_error() {
#ifdef _WIN32
    return WSAGetLastError();
#else
    return errno;
#endif
}

inline std::string error_string(int e) {
#ifdef _WIN32
    char* msg = nullptr;
    DWORD n = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                                 FORMAT_MESSAGE_IGNORE_INSERTS,
                             nullptr, (DWORD)e, 0, (char*)&msg, 0, nullptr);
    std::string out = n && msg ? std::string(msg, n) : ("error " + std::to_string(e));
    if (msg) LocalFree(msg);
    while (!out.empty() && (out.back() == '\n' || out.back() == '\r')) out.pop_back();
    return out;
#else
    return std::strerror(e);
#endif
}

[[noreturn]] inline void die(const char* what) {
    std::fprintf(stderr, "%s: %s\n", what, error_string(last_error()).c_str());
    std::exit(1);
}

// "The socket has nothing for you right now" - the return every non-blocking read
// gets when the receive queue is empty. EAGAIN/EWOULDBLOCK on POSIX.
inline bool would_block(int e) {
#ifdef _WIN32
    return e == WSAEWOULDBLOCK;
#else
    return e == EAGAIN || e == EWOULDBLOCK;
#endif
}

// A signal landed mid-syscall; retry. Windows has no such interruption, but WSAEINTR
// still exists, so check it anyway.
inline bool interrupted(int e) {
#ifdef _WIN32
    return e == WSAEINTR;
#else
    return e == EINTR;
#endif
}

// ---------------------------------------------------------------- startup

// Winsock needs an explicit init/teardown; everyone else needs nothing.
struct NetInit {
    NetInit() {
#ifdef _WIN32
        WSADATA d;
        if (WSAStartup(MAKEWORD(2, 2), &d) != 0) {
            std::fprintf(stderr, "WSAStartup failed\n");
            std::exit(1);
        }
#endif
    }
    ~NetInit() {
#ifdef _WIN32
        WSACleanup();
#endif
    }
    NetInit(const NetInit&) = delete;
    NetInit& operator=(const NetInit&) = delete;
};

// These servers are read through their output, so do not let it sit in a buffer
// when stdout is a pipe or a file. (MSVC has no line buffering; unbuffered is fine
// at this volume.)
inline void unbuffer_stdout() { setvbuf(stdout, nullptr, _IONBF, 0); }

// ---------------------------------------------------------------- ids

inline long process_id() {
#ifdef _WIN32
    return (long)GetCurrentProcessId();
#else
    return (long)getpid();
#endif
}

// The OS-level thread id - what /proc/<pid>/task/<tid>/ is keyed by on Linux, and
// what Process Explorer / Instruments show on Windows / macOS. glibc only exposes
// gettid() from 2.30 onward, so go straight to the syscall.
inline unsigned long long thread_id() {
#if defined(_WIN32)
    return (unsigned long long)GetCurrentThreadId();
#elif defined(__linux__)
    return (unsigned long long)syscall(SYS_gettid);
#elif defined(__APPLE__)
    uint64_t tid = 0;
    pthread_threadid_np(nullptr, &tid);
    return (unsigned long long)tid;
#else
    return 0;
#endif
}

// ---------------------------------------------------------------- logging

// Seconds since the first log line. The demo client's rhythm (a heartbeat every 5s)
// only means something if you can see WHEN the server reacted - or didn't.
inline double uptime_seconds() {
    static const std::chrono::steady_clock::time_point t0 = std::chrono::steady_clock::now();
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - t0).count();
}

// Every line carries the OS thread id, because the whole comparison is "which thread
// is doing what": the blocking server hands each connection its own tid, the epoll
// server does everything on one. Formatting into a single buffer and emitting it with
// ONE printf keeps lines from interleaving when several threads log at once.
inline void logf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    std::vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    std::printf("[%7.2fs][tid %-6llu] %s\n", uptime_seconds(), thread_id(), msg);
}

// Same, to stderr.
inline void elogf(const char* fmt, ...) {
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    std::vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    std::fprintf(stderr, "[%7.2fs][tid %-6llu] %s\n", uptime_seconds(), thread_id(), msg);
}

// ---------------------------------------------------------------- socket I/O

inline void close_socket(socket_t s) {
#ifdef _WIN32
    closesocket(s);
#else
    ::close(s);
#endif
}

// Sets the socket non-blocking: read()/accept() take the "timeo == 0" branch and
// return EAGAIN instead of installing a wait-entry and calling schedule().
// Same kernel code path as the blocking case, one branch different.
inline void set_nonblocking(socket_t s) {
#ifdef _WIN32
    u_long on = 1;
    if (ioctlsocket(s, FIONBIO, &on) != 0) die("ioctlsocket FIONBIO");
#else
    int f = fcntl(s, F_GETFL, 0);
    if (f < 0) die("fcntl F_GETFL");
    if (fcntl(s, F_SETFL, f | O_NONBLOCK) < 0) die("fcntl F_SETFL");
#endif
}

// Writing to a socket whose peer is gone raises SIGPIPE and kills the process by
// default. Linux suppresses it per-call with MSG_NOSIGNAL; the BSDs (macOS) do it
// per-socket with SO_NOSIGPIPE; Windows has no signals.
inline void suppress_sigpipe(socket_t s) {
#ifdef SO_NOSIGPIPE
    int on = 1;
    setsockopt(s, SOL_SOCKET, SO_NOSIGPIPE, (const char*)&on, sizeof(on));
#else
    (void)s;
#endif
}

#if defined(__linux__)
constexpr int kSendFlags = MSG_NOSIGNAL;
#else
constexpr int kSendFlags = 0;
#endif

// recv()/send() rather than read()/write(): they behave identically on sockets and
// exist on Windows too. Returned length is long long because POSIX says ssize_t and
// Winsock says int.
inline long long net_recv(socket_t s, char* buf, size_t len) {
#ifdef _WIN32
    return (long long)recv(s, buf, (int)len, 0);
#else
    return (long long)recv(s, buf, len, 0);
#endif
}

inline long long net_send(socket_t s, const char* buf, size_t len) {
#ifdef _WIN32
    return (long long)send(s, buf, (int)len, 0);
#else
    return (long long)send(s, buf, len, kSendFlags);
#endif
}

// ---------------------------------------------------------------- listener

inline socket_t make_listener(int port, int backlog) {
    socket_t fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd == kInvalidSocket) die("socket");

#ifndef _WIN32
    // Lets the port be reused while old connections linger in TIME_WAIT. Deliberately
    // not set on Windows, where SO_REUSEADDR means "steal a live socket" instead.
    int one = 1;
    setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, (const char*)&one, sizeof(one));
#endif

    sockaddr_in addr{};
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port = htons((unsigned short)port);

    if (bind(fd, (sockaddr*)&addr, sizeof(addr)) != 0) {
        std::fprintf(stderr, "bind: %s\n", error_string(last_error()).c_str());
        std::fprintf(stderr, "port %d is busy - is the other server still running?\n", port);
        std::exit(1);
    }
    if (listen(fd, backlog) != 0) die("listen");
    return fd;
}

inline std::string peer_name(const sockaddr_in& peer) {
    char ip[INET_ADDRSTRLEN] = {0};
    inet_ntop(AF_INET, (const void*)&peer.sin_addr, ip, sizeof(ip));
    return std::string(ip) + ":" + std::to_string(ntohs(peer.sin_port));
}

}  // namespace plat
