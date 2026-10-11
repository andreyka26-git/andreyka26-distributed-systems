# Blocking `read()` vs. epoll — two TCP echo servers (**Linux**)

Runnable companions to [socket-blocking-kernel.md](../socket-blocking-kernel.md).
Same protocol, same kernel wakeup primitive — different wait-queue entry.

This directory is **Linux only, on purpose**. There is no `platform.h` and no poller
abstraction: each `.cpp` is self-contained and calls `epoll_create1`, `epoll_ctl`,
`epoll_wait`, `accept`, `recv`, `fcntl(O_NONBLOCK)` and `gettid` directly, so what you
read is what the kernel is asked to do. (Same code with the portability layer:
[../cpp-crossplatform](../cpp-crossplatform). Windows' own version:
[../cpp-windows](../cpp-windows).)

| | [blocking_server.cpp](blocking_server.cpp) | [epoll_server.cpp](epoll_server.cpp) |
|---|---|---|
| threads | one per connection | one, total |
| idle connection costs | a `task_struct` asleep on `sk_wq` | an `epitem` in `ep->rbr` |
| `sk_wq` entry's `func` | `try_to_wake_up` | `ep_poll_callback` |
| sleeps inside | `read()` / `accept()` | `epoll_wait()` only |
| `wchan` while idle | `sk_wait_data` / `inet_csk_accept` | `ep_poll` |

Every log line either server prints is tagged `[seconds][tid N]`, because the whole
comparison is *which thread is doing what, and when*.

**Both servers listen on port 9000**, so you run one at a time and `demo_clients.py`
always dials the same place - no arguments, nothing to remember. Starting the second
while the first is up fails the bind and says so.

---

## Quick start

Install the toolchain, build, run. Nothing else is needed:

```sh
sudo apt update && sudo apt install -y build-essential cmake python3
cd AsyncAwaitKernel/cpp-linux
cmake -B build && cmake --build build
./build/epoll_server          # or ./build/blocking_server [--single-thread]
```

In a **second terminal**, run the demo client against it:

```sh
cd AsyncAwaitKernel/cpp-linux
python3 demo_clients.py                  # no arguments: both servers are on 9000
```

> No CMake? You don't actually need it — one file per server, no dependencies:
> ```sh
> g++ -std=c++17 -O2 -pthread -o epoll_server epoll_server.cpp
> g++ -std=c++17 -O2 -pthread -o blocking_server blocking_server.cpp
> ```

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
[   0.77s][tid 13] conn fd=4: parked in recv() - this thread is now asleep on this
                   socket's wait queue and can do NOTHING else
[   4.78s][tid 13] conn fd=4: peer closed
[   4.78s][tid 13] parked in accept() - waiting for a new connection
[   4.78s][tid 13] conn fd=4 127.0.0.1:5898: open, this thread now owns it (live=1)
[   4.78s][tid 13] conn fd=4: woke up with 27 bytes, echoing them back
```

The heartbeat's connection completed its three-way handshake immediately — the kernel
parked it on the listening socket's accept queue — but the thread was asleep in
`sk_wait_data` and never returned to `accept()`, so nothing ever read it. Its beats
piled up in `sk_receive_queue` and came back as one 27-byte lump the instant the silent
client sent FIN:

```
[   4.01s] heartbeat beat 1: echo came back after 4.005s  <-- stalled
[   4.01s] heartbeat beat 2: echo came back after 2.003s  <-- stalled
[   4.01s] heartbeat beat 3: echo came back after 0.001s
```

That stall is the drawback. Note *what* was expensive: not CPU — the thread was
`TASK_INTERRUPTIBLE` the whole time and burned none — but the fact that a task can only
ever be in one place at a time.

### 2. `blocking_server` — correct again, at one task per connection

The default calls `clone()`, so the heartbeat is served on time. Count the tids: **19**
parked in `inet_csk_accept`, **23** parked in `sk_wait_data` on a client that will never
send anything, and **24** doing the actual work.

```
[   0.67s][tid 19] parked in accept() - waiting for a new connection
[   0.67s][tid 23] conn fd=4 127.0.0.1:38807: open, this thread now owns it (live=1)
[   0.67s][tid 23] conn fd=4: parked in recv() - ... can do NOTHING else
[   0.71s][tid 24] conn fd=5 127.0.0.1:38808: open, this thread now owns it (live=2)
[   2.71s][tid 24] conn fd=5: woke up with 9 bytes, echoing them back
```

Task 23 exists solely because the silent client *might* one day say something.

### 3. `epoll_server` — one thread, both connections

The same tid on every line, including the beats that arrive while the silent connection
is still open:

```
[   0.71s][tid 28] conn fd=5 127.0.0.1:9910: open, no thread was created for it (live=1)
[   0.71s][tid 28] conn fd=6 127.0.0.1:9911: open, no thread was created for it (live=2)
[   0.71s][tid 28] epoll wait: sleeping, watching 2 connection(s) + the listener
[   2.71s][tid 28] epoll wait: woke with 1 ready fd(s)
[   2.71s][tid 28] conn fd=6: read 9 bytes, echoing them back
```

The silent connection is an `epitem` in `ep->rbr` that simply stops turning up on
`rdllist`. Nothing is parked on it, so it delays nobody — `VERDICT: the silent
connection cost the heartbeat nothing`.

## Edge-triggered variant

`EPOLLET`: a ready `epitem` is *not* re-added to `rdllist`, so the read loop **must**
drain to `EAGAIN` or the event is silently lost. `drain_and_echo()` already does; build
this way to compare the wakeup counts in the log.

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
9000 - and count its tasks while the demo is holding those connections:

```sh
./build/blocking_server &                             # then, in another terminal:
python3 demo_clients.py --idle 50 &
ls /proc/$(pgrep -x blocking_server)/task | wc -l     # 53
```
```sh
./build/epoll_server &                                # first server stopped
python3 demo_clients.py --idle 50 &
ls /proc/$(pgrep -x epoll_server)/task | wc -l        # 1
```

Measured: **53 vs 1** — 52 connections each time (50 idle plus the demo's two), so 52
tasks against 0. Same 52 wait-queue entries either way; the difference is whether they
point at a `task_struct` the scheduler must track or an `epitem` it never sees.
(`grep Vm /proc/<pid>/status` for the memory side.)

**Is the thread really asleep?** `S` = `TASK_INTERRUPTIBLE`, `%CPU` 0.0 — parked, not
spinning:

```sh
ps -L -o tid,stat,wchan:20,pcpu -p $(pgrep -x blocking_server)
```

`WCHAN` names the wait queue it's parked on: `sk_wait_data` (blocked in `read()`),
`inet_csk_accept` (accept loop), `ep_poll` (the epoll thread — on `ep->wq`, not on any
socket). `-` means your kernel doesn't export it; `S` + 0.0% still makes the point.

**Syscall shape:**

```sh
strace -f -e trace=epoll_wait,epoll_ctl,read,write ./build/epoll_server   # read() → EAGAIN, every time
strace -f -e trace=accept4,read,write,clone ./build/blocking_server       # clone() per conn; read() just hangs
```

> Under **WSL1** (`uname -r` ends in `-Microsoft`) syscalls are emulated: no
> `/proc/<pid>/wchan`, and `EPOLLET` is not honoured. Use **WSL2** — where the thread
> counts and the stall are real, though `wchan` may still read `-`.
