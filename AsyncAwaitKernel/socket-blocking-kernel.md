# Entities (one line each)

- **task** = a thread, `struct task_struct`, has a state: `RUNNING` (wants CPU) or `INTERRUPTIBLE` (sleeping).
- **scheduler** = code, not a thread. Runs inside whatever task is on the CPU when it calls `schedule()`; picks the next task and context-switches. Nobody schedules the scheduler.
- **run queue** = `struct rq`, one per CPU, owned by scheduler, holds only runnable tasks. The only list the scheduler scans. Sleepers are invisible to it.
- **wait queue** = a plain linked list owned by a resource (a socket), not the scheduler. Holds entries = `{func (callback), private (usually a task*)}`. Means "call `func` when this resource changes."
- **socket**: two separate lists — `sk_receive_queue` (the actual data, `sk_buff`s) and `sk_wq` (waiters/callbacks, no data).
- **NIC IRQ** = hardware interrupt when a packet lands. **softirq** = deferred kernel code the IRQ triggers; it hijacks the current CPU/thread to run the network receive path.
- **epoll** (`struct eventpoll`): rb-tree `rbr` (all monitored fds, one `epitem` each), ready list `rdllist` (`epitem`s that currently have an event — holds fds, not data), wait queue `ep->wq` (where an `epoll_wait` thread sleeps).

## Blocking `read()`

1. Thread runs `read(sk)` on CPU; `sk_receive_queue` empty. `O_NONBLOCK` unset → `timeo≠0`.
2. Kernel puts a wait-entry `{func=wake-me, private=this task}` on `sk_wq`, sets task `INTERRUPTIBLE`, calls `schedule()` → task leaves the CPU. 0 CPU burned (this is why we don't spin-wait for the interrupt).
3. Packet: NIC IRQ → softirq → appends data to `sk_receive_queue` → `sk_data_ready` → `wake_up(sk_wq)` walks that one socket's list and calls each entry's `func` (it's the waker; O(1), no global scan — the queue lives inside the socket, keyed by fd).
4. `func` = `try_to_wake_up`: sets task `RUNNING`, enqueues it on a run queue. Later `schedule()` (on some CPU) picks it; it resumes after `schedule()`, copies data, returns.

Non-blocking version (`O_NONBLOCK` or `MSG_DONTWAIT`, per-call): `timeo=0` → skip steps 2–4, return `-EAGAIN` immediately. Same code path, one branch.

## Non-blocking `read()` via epoll

**Setup** — `epoll_ctl(ADD, sk)`: make an `epitem`, insert in rb-tree. Call `sk`'s `poll()` passing a `poll_table` (envelope carrying epoll's installer, because only the socket knows where `sk_wq` is). `poll()` returns current readiness and runs the installer → `add_wait_queue(sk_wq, entry)` where `entry.func = ep_poll_callback`, `entry.private → epitem`. Same `sk_wq` as blocking `read`, different callback. N sockets = N such entries, each pointing at its own `epitem`.

**Loop:**

1. Thread: `read(sk)` → `EAGAIN` (you do not sleep in `read`). Back to `epoll_wait()`.
2. `epoll_wait`: `rdllist` empty → put a wait-entry `{private=this thread}` on `ep->wq`, sleep via `schedule()`.
3. Packet on `sk`: NIC IRQ → softirq → data to `sk_receive_queue` → `wake_up(sk_wq)` → calls that socket's entry's `func` = `ep_poll_callback` (still in softirq). It already knows which fd (via `private→epitem`) — no scanning. It: (a) appends `epitem` to `rdllist`, (b) `wake_up(ep->wq)` → sets the `epoll_wait` thread `RUNNING` + onto a run queue.
4. Scheduler runs that thread; `epoll_wait` drains `rdllist`. For each `epitem` it re-calls the fd's `poll()` to get the live mask, `&` interest → fills one `epoll_event` `{fd, mask}` per ready fd. Re-poll (not the stored entry) is how it knows which events (`EPOLLIN`=data or accept-ready; `EPOLLOUT`=writable; combined bits possible) and reflects state now, not when the IRQ fired.
5. Level-triggered: if still ready, `epitem` is re-added to `rdllist` (reported again next call). Edge-triggered (`EPOLLET`): not re-added.
6. Thread `read(sk)` until `EAGAIN`, loops to `epoll_wait`.

## The three facts that dissolve every earlier confusion

1. Wait queue ≠ run queue. Wait queue = per-resource "who to call on change," scheduler never scans it. Run queue = per-CPU runnable set, the only thing scheduler reads. Waking = move task from the former's effect (make `RUNNING`) into the latter.
2. Sleeping = removed from CPU contention, not spinning. The wait-entry is the only record of "who to wake"; the interrupt makes you runnable, it doesn't jump straight back into your code.
3. Same wakeup primitive both cases (`wake_up`→`func`→`try_to_wake_up`). Blocking `read`'s entry wakes you; epoll's entry runs `ep_poll_callback` first, then wakes the `epoll_wait` thread. `poll()`/`poll_table` are setup-only (install the callback); they're absent at wake time.
