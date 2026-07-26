# In-memory (plain Python) - pessimistic vs optimistic vs atomic

The "database" is a Python dict in one process. `N=8` threads race for one seat.

```bash
docker compose up --build
# or, no Docker needed:
python demo.py
```

## What it shows

| # | Scenario | Mechanism | Family | Loser experience |
|---|----------|-----------|--------|------------------|
| 1 | No lock | unguarded read-modify-write | (broken) | ❌ all "win" |
| 2 | Pessimistic | `threading.Lock` around critical section | **pessimistic** | **blocks**, then backs off |
| 3 | Optimistic | `version` + compare-and-swap retry loop | **optimistic** | CAS fails → **retry** |
| 4 | Atomic | `queue.Queue` single-token claim | **atomic** | claim fails → done |

## The mental model (same three across every folder)

- **Pessimistic** - *"I'll take the lock first, you wait."* Simple and correct under heavy
  contention, but waiters are blocked and you can deadlock if you're careless with lock order.
- **Optimistic** - *"I'll assume no conflict; if my version changed under me, I retry."*
  No blocking, scales well when conflicts are rare; wasted work (retries) when they're common.
- **Atomic** - *"One indivisible instruction does check-and-set."* No lock, no retry loop -
  but only works when the whole operation fits in one atomic primitive (CAS, `SETNX`, a
  conditional DB write).

> Note on the GIL: Python's GIL serializes bytecode, but it does **not** make a
> *check-then-act* atomic - the interpreter can switch threads at the `sleep` (or any
> bytecode boundary). That's exactly why scenario 1 still double-books.
