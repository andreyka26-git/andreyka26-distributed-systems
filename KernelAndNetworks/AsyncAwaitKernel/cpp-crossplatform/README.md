# Blocking `read()` vs. epoll — two TCP echo servers

Runnable companions to [socket-blocking-kernel.md](../socket-blocking-kernel.md).
Same protocol, same kernel wakeup primitive — different wait-queue entry.

| | [blocking_server.cpp](blocking_server.cpp) | [epoll_server.cpp](epoll_server.cpp) |
|---|---|---|
| threads | one per connection | one, total |
| idle connection costs | a `task_struct` asleep on `sk_wq` | an `epitem` in `ep->rbr` |
| `sk_wq` entry's `func` | `try_to_wake_up` | `ep_poll_callback` |
| sleeps inside | `read()` / `accept()` | `epoll_wait()` only |

Every log line either server prints is tagged `[seconds][tid N]`, because the whole
comparison is *which thread is doing what, and when*.

**Both servers listen on port 9000**, so you run one at a time and `demo_clients.py`
always dials the same place - no arguments, nothing to remember. Starting the second
while the first is up fails the bind and says so.

> **Want the native version instead?** This directory pays for portability with two
> abstraction headers. If you only care about one kernel, read
> [../cpp-linux](../cpp-linux) (raw epoll, `/proc`, `gettid` - no headers of its own) or
> [../cpp-windows](../cpp-windows) (straight Winsock + WSAPoll). Same servers, same
> ports, same `demo_clients.py`, same log lines.

Both build on Linux, macOS and Windows.
[crossplatform/platform.h](crossplatform/platform.h) hides the Winsock spelling
differences; [crossplatform/poller.h](crossplatform/poller.h) picks the readiness
backend — **epoll** on Linux, **kqueue** on macOS, **WSAPoll** on Windows. epoll and
kqueue are the same design; WSAPoll is `poll()` and rescans every fd per call, so it has
the right *shape* but not epoll's scaling (Windows' real answer, IOCP, is
completion-based and doesn't fit this comparison). The narrative below and in the
sources is epoll's.

---

## Quick start — Ubuntu / Debian

Install the toolchain, build, run. Nothing else is needed:

```sh
sudo apt update && sudo apt install -y build-essential cmake python3
cd AsyncAwaitKernel/cpp-crossplatform
cmake -B build && cmake --build build
./build/epoll_server          # or ./build/blocking_server [--single-thread]
```

In a **second terminal**, run the demo client against it:

```sh
cd AsyncAwaitKernel/cpp-crossplatform
python3 demo_clients.py                  # no arguments: both servers are on 9000
```

> No CMake? You don't actually need it — no dependencies:
> ```sh
> g++ -std=c++17 -O2 -pthread -o epoll_server epoll_server.cpp
> g++ -std=c++17 -O2 -pthread -o blocking_server blocking_server.cpp
> ```

## Quick start — Windows

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
cd AsyncAwaitKernel\cpp-crossplatform
cmake -B build
cmake --build build --config Release
.\build\Release\epoll_server.exe          # or blocking_server.exe [--single-thread]
```

In a **second terminal** (`python`, not `python3`, on Windows):

```powershell
cd AsyncAwaitKernel\cpp-crossplatform
python demo_clients.py
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

### 1. `blocking_server --single-thread` — the drawback

One thread, and the silent client owns it:

```
[   0.73s][tid 654] conn fd=4: parked in recv() - this thread is now asleep on this
                    socket's wait queue and can do NOTHING else
[   4.74s][tid 654] conn fd=4: peer closed
[   4.77s][tid 654] parked in accept() - waiting for a new connection
[   4.77s][tid 654] conn fd=4 127.0.0.1:40667: open, this thread now owns it (live=1)
[   4.77s][tid 654] conn fd=4: woke up with 27 bytes, echoing them back
```

The heartbeat's connection completed its TCP handshake immediately — the kernel parked
it in the accept queue — but the server never got back to `accept()`, so nothing ever
read it. Its beats piled up in `sk_receive_queue` and came back as one 27-byte lump the
instant the silent client sent FIN:

```
[   4.05s] heartbeat beat 1: echo came back after 4.031s  <-- stalled
[   4.05s] heartbeat beat 2: echo came back after 2.028s  <-- stalled
[   4.05s] heartbeat beat 3: echo came back after 0.023s
```

That stall is the drawback. Note *what* was expensive: not CPU — the thread burned none
while asleep — but the fact that a thread can only ever be in one place at a time.

### 2. `blocking_server` — correct again, at one thread per connection

The default spawns, so the heartbeat is served on time. Count the tids: **626** parked
in `accept()`, **630** parked in `recv()` on a client that will never send anything, and
**631** doing the actual work.

```
[   0.56s][tid 626] parked in accept() - waiting for a new connection
[   0.56s][tid 630] conn fd=4 127.0.0.1:27425: open, this thread now owns it (live=1)
[   0.57s][tid 630] conn fd=4: parked in recv() - ... can do NOTHING else
[   0.57s][tid 631] conn fd=5 127.0.0.1:27426: open, this thread now owns it (live=2)
[   2.57s][tid 631] conn fd=5: woke up with 9 bytes, echoing them back
```

Thread 630 exists solely because the silent client *might* one day say something.

### 3. `epoll_server` — one thread, both connections

The same tid on every line, including the beats that arrive while the silent connection
is still open:

```
[   0.57s][tid 504] conn fd=5 127.0.0.1:7274: open, no thread was created for it (live=1)
[   0.57s][tid 504] conn fd=6 127.0.0.1:7275: open, no thread was created for it (live=2)
[   0.57s][tid 504] epoll wait: sleeping, watching 2 connection(s) + the listener
[   2.57s][tid 504] epoll wait: woke with 1 ready fd(s)
[   2.57s][tid 504] conn fd=6: read 9 bytes, echoing them back
```

The silent connection is an `epitem` that simply stops turning up on `rdllist`. Nothing
is parked on it, so it delays nobody — `VERDICT: the silent connection cost the
heartbeat nothing`.

## Edge-triggered variant

`EPOLLET` on Linux, `EV_CLEAR` on macOS. Windows has no edge mode and the build will
deliberately refuse with a clear `#error`:

```sh
cmake -B build -DEDGE_TRIGGERED=ON && cmake --build build
```

---

## Flow

**Blocking** — `read()` on an empty socket (`timeo != 0`): kernel puts `{func=wake-me,
private=this task}` on `sk_wq`, marks the task `INTERRUPTIBLE`, calls `schedule()` — it
leaves the CPU, 0% burned. Packet: NIC IRQ → softirq → data to `sk_receive_queue` →
`wake_up(sk_wq)` → `try_to_wake_up` sets `RUNNING` + enqueues on a run queue → scheduler
picks it → `read()` returns.

**epoll** — `O_NONBLOCK` makes `read()` take the `timeo == 0` branch: `EAGAIN`, never
sleeps. `epoll_ctl(ADD)` put `ep_poll_callback` on that same `sk_wq` (via the socket's
`poll()` + `poll_table`) and an `epitem` in the rb-tree. One thread sleeps on `ep->wq`
inside `epoll_wait`. Packet: same IRQ → softirq → `wake_up(sk_wq)` → `ep_poll_callback`
pushes the `epitem` onto `rdllist`, *then* wakes the `epoll_wait` thread, which re-polls
each ready fd for its live mask and returns `{fd, events}`. Then `read()` until `EAGAIN`.

## Diagnostics

**What does an idle connection cost?** The point of the whole exercise. `--idle N` holds
N extra connections that do nothing at all. Run each server in turn - they share port
9000 - and count its threads while the demo is holding those connections.

Ubuntu:

```sh
./build/blocking_server &                             # then, in another terminal:
python3 demo_clients.py --idle 50 &
ls /proc/$(pgrep -x blocking_server)/task | wc -l     # 53
# stop it, start ./build/epoll_server, repeat:
ls /proc/$(pgrep -x epoll_server)/task | wc -l        # 1
```

Windows:

```powershell
Start-Process python -ArgumentList "demo_clients.py --idle 50"
(Get-Process blocking_server).Threads.Count           # 56
# stop it, start epoll_server.exe, repeat:
(Get-Process epoll_server).Threads.Count              # 4
```

Measured: **53 vs 1** on Linux, **56 vs 4** on Windows — 52 connections each time (50
idle plus the demo's two). The C runtime keeps a few threads of its own on Windows, so
compare the *delta*, 52 vs 0, not the absolutes. Same 52 wait-queue entries either way;
the difference is whether they point at a task the scheduler must track or an `epitem`
it never sees. (`grep Vm /proc/<pid>/status` for the memory side.)

**Is the thread really asleep?** *(Linux)* `S` = `TASK_INTERRUPTIBLE`, `%CPU` 0.0 —
parked, not spinning:

```sh
ps -L -o tid,stat,wchan:20,pcpu -p $(pgrep -x blocking_server)
```

`WCHAN` names the wait queue it's parked on: `sk_wait_data` (blocked in `read()`),
`inet_csk_accept` (accept loop), `ep_poll` (the epoll thread — on `ep->wq`, not any
socket). `-` means your kernel doesn't export it; `S` + 0.0% still makes the point.

**Syscall shape:** *(Linux `strace`, macOS `dtruss -f`, Windows Process Monitor)*

```sh
strace -f -e trace=epoll_wait,epoll_ctl,read,write ./build/epoll_server   # read() → EAGAIN, every time
strace -f -e trace=accept4,read,write,clone ./build/blocking_server       # clone() per conn; read() just hangs
```

> On WSL1 (`uname -r` ends in `-Microsoft`) syscalls are emulated: no
> `/proc/<pid>/wchan`, and `EPOLLET` is not honoured. Thread counts are still real.
> Use WSL2 — or just build natively on Windows.
