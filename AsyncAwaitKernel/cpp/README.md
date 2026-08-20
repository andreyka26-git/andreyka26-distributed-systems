# Blocking `read()` vs. epoll — two TCP echo servers

Runnable companions to [socket-blocking-kernel.md](../socket-blocking-kernel.md).
Same protocol, same kernel wakeup primitive — different wait-queue entry.

| | [blocking_server.cpp](blocking_server.cpp) (:9001) | [epoll_server.cpp](epoll_server.cpp) (:9002) |
|---|---|---|
| threads | one per connection | one, total |
| idle connection costs | a `task_struct` asleep on `sk_wq` | an `epitem` in `ep->rbr` |
| `sk_wq` entry's `func` | `try_to_wake_up` | `ep_poll_callback` |
| sleeps inside | `read()` / `accept()` | `epoll_wait()` only |

Both build on Linux, macOS and Windows. [platform.h](platform.h) hides the Winsock
spelling differences; [poller.h](poller.h) picks the readiness backend — **epoll** on
Linux, **kqueue** on macOS, **WSAPoll** on Windows. epoll and kqueue are the same
design; WSAPoll is `poll()` and rescans every fd per call, so it has the right *shape*
but not epoll's scaling (Windows' real answer, IOCP, is completion-based and doesn't
fit this comparison). The narrative below and in the sources is epoll's.

---

## Quick start — Ubuntu / Debian

Install the toolchain, build, run. Nothing else is needed:

```sh
sudo apt update && sudo apt install -y build-essential cmake python3
cd AsyncAwaitKernel/cpp
cmake -B build && cmake --build build
./build/epoll_server          # or ./build/blocking_server
```

In a **second terminal**, talk to it:

```sh
cd AsyncAwaitKernel/cpp
python3 echo_client.py 9002              # epoll_server  (blocking_server is 9001)
python3 echo_client.py 9002 --big        # 200 KB, exercises the partial-write path
```

> No CMake? You don't actually need it — two files, no dependencies:
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
cd AsyncAwaitKernel\cpp
cmake -B build
cmake --build build --config Release
.\build\Release\epoll_server.exe          # or blocking_server.exe
```

In a **second terminal** (`python`, not `python3`, on Windows):

```powershell
cd AsyncAwaitKernel\cpp
python echo_client.py 9002
python echo_client.py 9002 --big
```

> Alternative that avoids the PATH dance entirely: open **Developer PowerShell for
> VS 2022** from the Start menu (or the `+` dropdown in the VS Code terminal panel) —
> it has `cmake` and `cl` already on PATH.

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

**What does an idle connection cost?** The point of the whole exercise. Start *both*
servers, hold 50 connections open against each with
[idle_clients.py](idle_clients.py), then count the servers' threads.

Ubuntu:

```sh
python3 idle_clients.py 9001 50 &        # against blocking_server
python3 idle_clients.py 9002 50 &        # against epoll_server
ls /proc/$(pgrep -f blocking_server)/task | wc -l     # 51
ls /proc/$(pgrep -f epoll_server)/task | wc -l        # 1
```

Windows:

```powershell
Start-Process python -ArgumentList "idle_clients.py 9001 50"
Start-Process python -ArgumentList "idle_clients.py 9002 50"
(Get-Process blocking_server).Threads.Count           # 54
(Get-Process epoll_server).Threads.Count              # 4
```

Measured: **51 vs 1** on Linux, **54 vs 4** on Windows — the C runtime keeps a few
threads of its own there, so compare the *delta* (50 vs 0), not the absolutes. Same 50
wait-queue entries either way; the difference is whether they point at a task the
scheduler must track or an `epitem` it never sees. (`grep Vm /proc/<pid>/status` for
the memory side.)

**Is the thread really asleep?** *(Linux)* `S` = `TASK_INTERRUPTIBLE`, `%CPU` 0.0 —
parked, not spinning:

```sh
ps -L -o tid,stat,wchan:20,pcpu -p $(pgrep -f blocking_server)
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
