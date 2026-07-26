# Seat reservation across Postgres, Redis & MongoDB — the same race, three stores

A production-style .NET 8 take on the classic seat-reservation race. One operation, `lock_seat`
("reserve this seat for exactly one customer"), implemented against **three data stores** behind a
single strategy-pattern interface, [`ISeatLocker`](src/SeatLocking.cs). Each store exposes a family
of interchangeable algorithms — some deliberately broken to *show* a lost update, some safe — and one
runner ([src/Program.cs](src/Program.cs)) drives them all, parking two customers and releasing them
together to force the race.

## The strategies

Every strategy is a `SeatLockStrategy` (name + expectation + delegate) returned by a locker's
`Strategies` list. `ExpectedSafe` tells the runner whether it should see exactly one winner.

### Postgres — [src/PostgresSeatLocker.cs](src/PostgresSeatLocker.cs) (Dapper)

| Method                          | Safe? | How it behaves                                                        |
|---------------------------------|:-----:|----------------------------------------------------------------------|
| `LockSeatDirtyWriteAsync`       |  ❌   | naive read-modify-write, keyed on `id` only → **lost update**        |
| `LockSeatPessimisticAsync`      |  ✅   | `SELECT ... FOR UPDATE` row lock; the loser **blocks**, then backs off |
| `LockSeatOptimisticAsync`       |  ✅   | compare-and-swap: `UPDATE ... WHERE status = @s` → 0 rows means you lost |
| `LockSeatRepeatableReadAsync`   |  ✅   | same naive write, but snapshot isolation aborts the loser with `40001`|

> **Every Postgres method opens an explicit transaction**, including the read-only ones, so the
> isolation level in play is always visible at the call site rather than implied.
>
> **Nothing retries.** Losing the race isn't a transient failure — it's the answer. `available →
> reserved` is one-way, so a retry could only re-read the same holder. Each loser reads the winner
> once and returns `AlreadyTaken`. Under READ COMMITTED that re-read happens inside the same
> transaction (fresh snapshot per statement); under REPEATABLE READ it needs a second transaction,
> since the first one is aborted *and* its snapshot still shows `available`.

### Redis — [src/RedisSeatLocker.cs](src/RedisSeatLocker.cs) (StackExchange.Redis)

A seat is a string key `seat:{id}` (absent = available, value = holder). Redis runs each command / Lua
script atomically on one thread, so the fix is never "take a lock" — it's "collapse read + write into
one atomic step".

| Method                    | Safe? | How it behaves                                                          |
|---------------------------|:-----:|------------------------------------------------------------------------|
| `LockSeatNaiveSetAsync`   |  ❌   | `GET`, decide in app code, then unconditional `SET` → **lost update**  |
| `LockSeatSetNxAsync`      |  ✅   | `SET key val NX` — one atomic set-if-absent; exactly one caller wins    |
| `LockSeatLuaAsync`        |  ✅   | the whole `EXISTS`+`SET` packed into one server-side Lua script         |

### MongoDB — [src/MongoSeatLocker.cs](src/MongoSeatLocker.cs) (MongoDB.Driver)

A seat is one document `{ _id, status, reserved_by, version }`. A **single-document** write is atomic
and isolated, but that atomicity does *not* span a separate read + separate write.

| Method                  | Safe? | How it behaves                                                             |
|-------------------------|:-----:|---------------------------------------------------------------------------|
| `LockSeatNaiveAsync`    |  ❌   | find, decide, then update by `_id` only → **lost update**                 |
| `LockSeatAtomicAsync`   |  ✅   | one conditional update guarded by `status` (optimistic CAS)               |
| `LockSeatReadStatusGuardAsync` | ✅ | same read-then-check as naive, but the filter carries the status we read |

> **Optimistic vs pessimistic in Mongo:** there's no `SELECT ... FOR UPDATE`. The optimistic route is
> to put the expected state (`status = 'available'`) into the update's filter — a stale write matches
> 0 docs, and the loser re-reads to report the winner rather than retrying. The closest pessimistic
> tool is `findAndModify` (read+write one doc atomically); true multi-document "lock now, decide
> later" needs a replica-set **transaction** with retries on `TransientTransactionError`.

## What this does *not* model (read before copying into a real ticketing system)

Every strategy here is correct for the domain as modelled: **one seat, one row, `available → reserved`,
one-way and permanent.** That last property is load-bearing — it's why a status guard is as strong as a
version guard, and why no strategy retries. A real ticketing domain breaks it, and these are the gaps
that opens:

| Gap | Why it matters | What it needs |
|---|---|---|
| **No hold expiry** | Real carts hold a seat for N minutes, then release it. That makes the lifecycle a *cycle* (`available → held → available`), not one-way. Abandoned carts here lock a seat forever — `SET NX` with no `PX` never expires. | `hold_expires_at` + a predicate like `WHERE status='available' OR hold_expires_at < now()`; `SET key val NX PX <ttl>` in Redis. |
| **No ownership check on confirm/release** | Once holds expire, `WHERE status='held'` no longer proves *you* are the holder. If your hold lapsed and someone else took the seat, confirming your purchase would silently steal theirs — the genuine ABA case. `ResetSeatAsync` deletes the Redis key unconditionally, which as a *release* would free somebody else's hold. | Guard on identity, not status: `WHERE hold_id = @mine` / `version = @v`. The `version` column exists here but no strategy actually guards on it. |
| **Not idempotent** | Removing the *internal* retries was right, but the network edge still retries (timeouts, 502s, double-clicks). If your reservation committed and the response was lost, your retry reads `reserved` and is told `AlreadyTaken` — about your own seat. | An idempotency key per request; return `Reserved` when the current holder is already you. |
| **Single-seat only** | "4 seats together" is all-or-nothing across 4 rows. | Postgres: one transaction, `FOR UPDATE` in a **consistent seat order** or you trade lost updates for `40P01` deadlocks. Redis: one Lua script over all keys (same hash slot in Cluster). Mongo: a multi-document transaction — **the `mongo` container here is a standalone, so it can't do them at all** (verified: `startTransaction` fails). |
| **Pessimistic doesn't survive a hot event** | `FOR UPDATE` on one row serializes every request behind one lock. That is fine for two customers and collapses under an onsale stampede. | Waiting room / queue in front, then optimistic or Redis-fronted allocation. |

## Run the demo

```bash
docker compose up --build
```

The `app` container runs every strategy against every store and self-reports, e.g.:

```
# BACKEND: Redis
Redis NAIVE SET ...   DOUBLE BOOKING: 2 customers were told 'you got it'.  (as expected — broken strategy demonstrates the lost update)
Redis SET NX   ...    CORRECT: exactly one customer got the seat.          (as expected — safe strategy)
Redis LUA      ...    CORRECT: exactly one customer got the seat.          (as expected — safe strategy)
```

The broken strategies race in-process; an occasional run may not interleave and will print an
"unexpected outcome — rerun" note. The `app` container exits when done; the stores stay up so you can
poke them.

## Run locally (without containerizing the app)

Start just the stores, then run the .NET app on the host:

```bash
docker compose up -d postgres redis mongo
dotnet run --project src
```

Defaults (override with the matching env var):

| Store    | Env var               | Default (host)                                              |
|----------|-----------------------|------------------------------------------------------------|
| Postgres | `POSTGRES_CONNECTION` | `Host=localhost;Port=5434;Database=seats;Username=postgres;Password=postgres` |
| Redis    | `REDIS_CONNECTION`    | `localhost:6380`                                           |
| MongoDB  | `MONGO_CONNECTION`    | `mongodb://localhost:27018`                                |

## Naming / ports (parallel-safe)

This stack is namespaced so it can run **at the same time** as the sibling demos. Host ports are
offset off the defaults to dodge any locally-running server:

| Thing            | This folder (`dotnet-production-example`)                                  |
|------------------|---------------------------------------------------------------------------|
| host ports       | Postgres **5434**, Redis **6380**, Mongo **27018**                        |
| container names  | `concurrency-pg-dotnet`, `concurrency-redis-dotnet`, `concurrency-mongo-dotnet`, `concurrency-app-dotnet` |

The Postgres schema is created and seeded automatically by [sql/00_setup.sql](sql/00_setup.sql) on
first boot; Redis and Mongo need no init — the app resets the seat before every scenario.

Tear down with `docker compose down -v`.

> **Pessimistic** = "I'll wait my turn" — best under high contention on the same row/doc.
> **Optimistic** = "I'll try and apologize if I lose" — best when conflicts are rare.
> **Atomic single-op** (SET NX / Lua / conditional update) = "make the check and the write indivisible".
