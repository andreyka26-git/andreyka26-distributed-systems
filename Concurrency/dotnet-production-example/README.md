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
| `LockSeatOptimisticAsync`       |  ✅   | versioned compare-and-swap: `UPDATE ... WHERE status = @s` → retry    |
| `LockSeatRepeatableReadAsync`   |  ✅   | same naive write, but snapshot isolation aborts the loser with `40001`|

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
| `LockSeatAtomicAsync`   |  ✅   | one conditional update guarded by `version` (optimistic CAS), retry       |

> **Optimistic vs pessimistic in Mongo:** there's no `SELECT ... FOR UPDATE`. The optimistic route is
> to put the expected state (`version = @v`) into the update's filter — a stale write matches 0 docs
> and retries. The closest pessimistic tool is `findAndModify` (read+write one doc atomically); true
> multi-document "lock now, decide later" needs a replica-set **transaction** with retries on
> `TransientTransactionError`.

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
