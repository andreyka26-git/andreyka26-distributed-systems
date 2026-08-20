# Blocking `recv()` vs. WSAPoll — two TCP echo servers (**Windows**)

Runnable companions to [socket-blocking-kernel.md](../socket-blocking-kernel.md).
Same protocol, same wakeup primitive — different thing waiting on the endpoint.

This directory is **Windows only, on purpose**. There is no `platform.h` and no poller
abstraction: each `.cpp` is self-contained Winsock and calls `WSAStartup`, `accept`,
`recv`, `ioctlsocket(FIONBIO)`, `WSAPoll`, `WSAGetLastError` and `GetCurrentThreadId`
directly. (Same code with the portability layer:
[../cpp-crossplatform](../cpp-crossplatform). Linux's own version, with real epoll:
[../cpp-linux](../cpp-linux).)

| | [blocking_server.cpp](blocking_server.cpp) | [epoll_server.cpp](epoll_server.cpp) |
|---|---|---|
| threads | one per connection | one, total |
| idle connection costs | a thread waiting on the endpoint | one `WSAPOLLFD` in an array |
| sleeps inside | `recv()` / `accept()` | `WSAPoll()` only |
| non-blocking mode | — | `ioctlsocket(FIONBIO)`, `WSAEWOULDBLOCK` |

Every log line either server prints is tagged `[seconds][tid N]`, because the whole
comparison is *which thread is doing what, and when*.

**Both servers listen on port 9000**, so you run one at a time and `demo_clients.py`
always dials the same place - no arguments, nothing to remember. Starting the second
while the first is up fails the bind and says so.

> **`epoll_server.cpp` on Windows?** The file keeps that name because that is what the
> diagram and the walkthrough call it, but epoll is Linux's. Windows'
> readiness API is **WSAPoll**, and the difference matters: epoll
> registers each socket *once* and a wakeup pushes the ready fd onto a ready list
> (`epoll_wait` costs O(ready)), while WSAPoll is `poll()` — the whole array is copied
> into the kernel and scanned on **every** call (O(watched)). So this code has the right
> *shape* — one thread, no thread per connection, sleeps in exactly one place — and
> makes the same point against `blocking_server.cpp`, but not epoll's scaling claim.
> Windows' real answer to that is **IOCP**, which is completion-based (you hand the
> kernel a buffer and it tells you when the copy is *done*) rather than readiness-based,
> so it doesn't fit this side-by-side at all.

---

## Quick start

You need the **MSVC C++ toolchain**. Visual Studio bundles both it and CMake, but puts
neither on your PATH. Run this first — it finds them whatever edition/year you have,
and puts `cmake` on PATH for this terminal:

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$cmakeExe = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.CMake.Project -find "**\CMake\bin\cmake.exe" | Select-Object -First 1
$env:PATH = "$(Split-Path $cmakeExe);$env:PATH"
cmake --version
```

If that prints a version, **you already have everything — skip to the build below.**
If it prints nothing, you need the toolchain. Either download and run
[vs_BuildTools.exe](https://aka.ms/vs/17/release/vs_BuildTools.exe) and tick *Desktop
development with C++* (which includes *C++ CMake tools for Windows*), or use winget:

```powershell
winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.CMake.Project"
```

> `winget` itself "not recognized"? On Windows 10 1709+ / Windows 11 it is installed but
> its folder is sometimes missing from PATH. Restore it with
> `$env:PATH += ";$env:LOCALAPPDATA\Microsoft\WindowsApps"`, or just use the direct
> download above.

Now build and run. MSVC is multi-config, so the config must be named, and binaries land
in `build\Release\`:

```powershell
cd AsyncAwaitKernel\cpp-windows
cmake -B build
cmake --build build --config Release
.\build\Release\epoll_server.exe          # or blocking_server.exe [--single-thread]
```

In a **second terminal** (`python`, not `python3`, on Windows):

```powershell
cd AsyncAwaitKernel\cpp-windows
python demo_clients.py                    # no arguments: both servers are on 9000
```

> Alternative that avoids the PATH dance entirely: open **Developer PowerShell for
> VS 2022** from the Start menu (or the `+` dropdown in the VS Code terminal panel) —
> it has `cmake` and `cl` already on PATH.

---

## The demonstration

[demo_clients.py](demo_clients.py) opens exactly two connections:

- **silent** — echoes one message, then never speaks again *and does not disconnect*.
  The server is left holding a connection that will never become readable.
- **heartbeat** — connects second, then echoes `echo2 #n` every 5 seconds. It is the
  measuring stick: its round trips are flat on a server that can serve two connections
  at once, and they are not on a server that cannot.

It is server-agnostic: both servers are on port 9000 and it dials port 9000, so the
same command works for whichever one you have running.

After 3 beats the silent client disconnects, so you can see what its departure releases
(`--hold-beats` >= `--beats` keeps it connected for the whole run).
Run each server against it and read the timestamps and the tids.

### 1. `blocking_server.exe --single-thread` — the drawback

One thread, and the silent client owns it:

```
[   0.88s][tid 26824] conn fd=272: parked in recv() - this thread is now asleep on this
                      socket's endpoint and can do NOTHING else
[   5.01s][tid 26824] conn fd=272: peer closed
[   5.10s][tid 26824] parked in accept() - waiting for a new connection
[   5.14s][tid 26824] conn fd=164 127.0.0.1:53744: open, this thread now owns it (live=1)
[   5.18s][tid 26824] conn fd=164: woke up with 27 bytes, echoing them back
```

The heartbeat's connection completed its three-way handshake immediately — Windows
parked it on the listening socket's accept queue — but the thread was waiting inside
`recv()` and never returned to `accept()`, so nothing ever read it. Its beats piled up
in the receive buffer and came back as one 27-byte lump the instant the silent client
sent FIN:

```
[   4.44s] heartbeat beat 1: echo came back after 4.187s  <-- stalled
[   4.44s] heartbeat beat 2: echo came back after 2.187s  <-- stalled
[   4.44s] heartbeat beat 3: echo came back after 0.172s
```

That stall is the drawback. Note *what* was expensive: not CPU — the thread was in
`Wait:UserRequest` the whole time and burned none — but the fact that a thread can only
ever be in one place at a time.

### 2. `blocking_server.exe` — correct again, at one thread per connection

The default spawns, so the heartbeat is served on time. Count the tids: **7320** waiting
in `accept()`, **7384** waiting in `recv()` on a client that will never send anything,
and **38492** doing the actual work.

```
[   0.79s][tid 7320 ] parked in accept() - waiting for a new connection
[   0.79s][tid 7384 ] conn fd=244 127.0.0.1:53755: open, this thread now owns it (live=1)
[   0.89s][tid 7384 ] conn fd=244: parked in recv() - ... can do NOTHING else
[   1.02s][tid 38492] conn fd=252 127.0.0.1:53756: open, this thread now owns it (live=2)
[   1.12s][tid 38492] conn fd=252: woke up with 9 bytes, echoing them back
```

Thread 7384 exists solely because the silent client *might* one day say something — a
kernel stack, an `ETHREAD`, scheduler bookkeeping and a 1 MB reserved user stack, all
to wait.

### 3. `epoll_server.exe` (WSAPoll) — one thread, both connections

The same tid on every line, including the beats that arrive while the silent connection
is still open:

```
[   0.75s][tid 34008] conn fd=256 127.0.0.1:64728: open, no thread was created for it (live=1)
[   0.97s][tid 34008] conn fd=284 127.0.0.1:64729: open, no thread was created for it (live=2)
[   0.99s][tid 34008] WSAPoll wait: sleeping, watching 2 connection(s) + the listener
[   2.94s][tid 34008] WSAPoll wait: woke with 1 ready fd(s)
[   3.00s][tid 34008] conn fd=284: read 9 bytes, echoing them back
```

The silent connection is one entry in the `WSAPOLLFD` array that never comes back ready.
Nothing is parked on it, so it delays nobody — `VERDICT: the silent connection cost the
heartbeat nothing`.

## No edge-triggered variant here

`WSAPoll` is level-triggered and has no alternative: epoll's `EPOLLET` and kqueue's
`EV_CLEAR` have no Windows equivalent, so anything still ready is simply reported again
on the next call. The build deliberately offers no `EDGE_TRIGGERED` option — see
[../cpp-linux](../cpp-linux) for that comparison.

---

## Flow

**Blocking** — `recv()` with nothing buffered: `afd.sys` queues an IRP on the endpoint
and the calling thread waits on a kernel dispatcher object; the thread leaves the CPU
entirely (`KeWaitForSingleObject`, state *Waiting*), 0% burned. Packet: NIC DPC →
`tcpip.sys` appends to the receive buffer → completes the IRP → `KeSetEvent` wakes
exactly the thread waiting on *that* endpoint — no scan — → scheduler makes it *Ready*,
then *Running* → `recv()` copies to userspace and returns.

**WSAPoll** — `ioctlsocket(FIONBIO)` makes `recv()` return `WSAEWOULDBLOCK` instead of
waiting. One thread sleeps inside `WSAPoll`; the array of watched sockets is copied into
the kernel, each endpoint is checked, and the thread is released when any of them is
ready. `revents` then says what is ready *now*, and `recv()` runs until
`WSAEWOULDBLOCK`.

The kernel-side wakeup is the same event either way. What changes is *what* was waiting
on it: a thread of yours, or one entry in an array that one thread is polling on behalf
of every connection.

## Diagnostics

**What does an idle connection cost?** The point of the whole exercise. `--idle N` holds
N extra connections that do nothing at all. Run each server in turn - they share port
9000 - and count its threads while the demo is holding those connections:

```powershell
.\build\Release\blocking_server.exe                    # then, in another terminal:
Start-Process python -ArgumentList "demo_clients.py --idle 50"
(Get-Process blocking_server).Threads.Count           # 56
```
```powershell
.\build\Release\epoll_server.exe                       # first server stopped
Start-Process python -ArgumentList "demo_clients.py --idle 50"
(Get-Process epoll_server).Threads.Count              # 4
```

Measured: **56 vs 4** — 52 connections each time (50 idle plus the demo's two). The C
runtime keeps a few threads of its own, so compare the *delta*, 52 vs 0, not the
absolutes.

**Are those threads really asleep?** They burn no CPU — Task Manager shows the process
flat at 0%. For the detail, open **Process Explorer** → double-click the process →
*Threads* tab: every per-connection thread sits in `Wait:UserRequest` with a stack
ending in `ntoskrnl!KeWaitForSingleObject` under `afd.sys`. The single WSAPoll thread
waits the same way — there is just one of it.

```powershell
(Get-Process blocking_server).Threads |
  Select-Object Id, ThreadState, WaitReason, TotalProcessorTime
```

**Syscall shape:** Windows has no `strace`. Use **Process Monitor** (filter
*Operation* → *begins with* `TCP`) to watch `TCP Receive` / `TCP Send` per connection,
or the **Windows Performance Recorder** for a scheduler trace showing threads parked on
their endpoints.
