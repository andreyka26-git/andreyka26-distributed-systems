# Redis — atomic operations & optimistic transactions

```bash
docker compose up --build
```

## What it shows

| # | Scenario | Mechanism | Family | Result |
|---|----------|-----------|--------|--------|
| 1 | GET then SET | two commands, gap between them | (broken) | ❌ double booking |
| 2 | `SET k v NX` | atomic set-if-absent | **atomic** | ✅ one winner |
| 3 | Lua script | whole check-and-set runs uninterrupted | **atomic** | ✅ one winner |
| 4 | `WATCH`/`MULTI`/`EXEC` | abort txn if key changed | **optimistic** | ✅ one winner |

## Why these work

Redis executes commands **single-threaded**, one at a time. So:

- **A single command is atomic.** `SET ... NX` does the "is it free?" check *and* the set
  in one indivisible step — the race window in scenario 1 simply doesn't exist. (`SETNX` is
  the legacy dedicated command; `SET k v NX EX 30` is the modern form, and adding `EX` gives
  the lock a TTL so a crashed holder doesn't deadlock everyone.)
- **A Lua script is atomic.** Use it when "check-and-set" is *more than one command*
  (here: `HGET` status, then `HSET`). Nothing else runs mid-script.
- **`WATCH` is optimistic.** `EXEC` only applies the queued commands if no watched key
  changed since `WATCH`; otherwise it returns nil → `WatchError` → you retry. This is
  compare-and-swap at the key level.

> Redis has **no pessimistic blocking lock primitive** built in. "Locks" in Redis are built
> *from* the atomic `SET NX EX` (single instance) or the Redlock algorithm (multi instance) —
> i.e. they're atomic/optimistic constructions, not the server blocking a waiter.

Poke it:
```bash
docker compose exec redis redis-cli HGETALL seat:hash:1
```
