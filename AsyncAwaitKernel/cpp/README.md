# Blocking `read()` vs. epoll — two TCP echo servers

Runnable companions to [socket-blocking-kernel.md](../socket-blocking-kernel.md).
Same protocol, same kernel wakeup primitive — different wait-queue entry.

| | [blocking_server.cpp](blocking_server.cpp) (:9001) | [epoll_server.cpp](epoll_server.cpp) (:9002) |
|---|---|---|
| threads | one per connection | one, total |
| idle connection costs | a `task_struct` asleep on `sk_wq` | an `epitem` in `ep->rbr` |
| `sk_wq` entry's `func` | `try_to_wake_up` | `ep_poll_callback` |
| sleeps inside | `read()` / `accept()` | `epoll_wait()` only |

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

## Setup

Linux only. On Windows use WSL — `make`/`g++`/`epoll` don't exist in PowerShell:

```powershell
wsl
cd /mnt/c/Projects/andreyka26-distributed-systems/AsyncAwaitKernel/cpp
```

```sh
make                        # blocking_server, epoll_server, epoll_server_et (EPOLLET)
./blocking_server &         # or ./epoll_server
printf 'hi\n' | nc -q1 127.0.0.1 9001
```

## Diagnostics

**Is the thread really asleep?** `S` = `TASK_INTERRUPTIBLE`, `%CPU` 0.0 — parked, not spinning:

```sh
ps -L -o tid,stat,wchan:20,pcpu -p $(pgrep -f ./blocking_server)
```

`WCHAN` names the wait queue it's parked on: `sk_wait_data` (blocked in `read()`),
`inet_csk_accept` (accept loop), `ep_poll` (the epoll thread — on `ep->wq`, not any
socket). `-` means your kernel doesn't export it; `S` + 0.0% still makes the point.

**What does an idle connection cost?** Hold 500 open, count kernel tasks:

```sh
python3 idle_clients.py 9001 500                        # then, in another shell:
ls /proc/$(pgrep -f ./blocking_server)/task | wc -l      # ~501
ls /proc/$(pgrep -f ./epoll_server)/task | wc -l         # 1
```

Measured with 50 idle connections: **51 tasks** vs **1**. Same 50 wait-queue entries
either way — the difference is whether they point at a task the scheduler must track or
an `epitem` it never sees. (`grep Vm /proc/<pid>/status` for the memory side.)

**Syscall shape:**

```sh
strace -f -e trace=epoll_wait,epoll_ctl,read,write ./epoll_server   # read() → EAGAIN, every time
strace -f -e trace=accept4,read,write,clone ./blocking_server       # clone() per conn; read() just hangs
```

> On WSL1 (`uname -r` ends in `-Microsoft`) syscalls are emulated: no `/proc/<pid>/wchan`,
> and `EPOLLET` is not honoured. Thread counts are still real. Use WSL2 for the rest.
