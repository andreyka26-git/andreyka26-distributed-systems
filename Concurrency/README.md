# Concurrency & Race Condition POCs — "Reserve the seat"

A set of runnable demos that all solve the **same** problem in different stores:

> Two (or more) customers try to reserve the **same seat** at the same time.
> Exactly **one** must win. Nobody should ever be double-booked.

Each folder shows the **BUG** (the race condition / double booking) *and* the **FIX**,
using the three families of concurrency control:

| Strategy        | Idea                                                                 | "Who blocks?"            |
|-----------------|---------------------------------------------------------------------|--------------------------|
| **Pessimistic** | Take a lock *before* you touch the row. Others **wait**.            | losers wait, then bail   |
| **Optimistic**  | Don't lock. Write *conditionally* (`WHERE version = x`). Loser fails | losers fail, then retry  |
| **Atomic**      | One indivisible operation does check-and-set (`SETNX`, Lua, CAS).   | nobody waits, one wins   |

## Folders

| Folder            | Store            | Strategies shown                                              |
|-------------------|------------------|--------------------------------------------------------------|
| [`postgres/`](postgres/)   | PostgreSQL       | lost update bug, **optimistic** (version), **pessimistic** (`FOR UPDATE`), isolation levels (`READ COMMITTED` / `REPEATABLE READ` / `SERIALIZABLE`). **+ raw `.sql` files for pgAdmin.** |
| [`mongodb/`](mongodb/)     | MongoDB          | bug, **optimistic** (atomic conditional update / version), **pessimistic** (transactions → WriteConflict). |
| [`dynamodb/`](dynamodb/)   | DynamoDB (local) | bug, **optimistic** (`ConditionExpression` + version). No native pessimistic — explained. |
| [`redis/`](redis/)         | Redis            | bug, **atomic** `SET NX`, **atomic** Lua script, **optimistic** `WATCH`/`MULTI`/`EXEC`. |
| [`inmemory/`](inmemory/)   | Plain Python     | bug, **pessimistic** (`Lock`), **optimistic** (CAS retry), **atomic** (`Queue`). |

## How to run one

Each folder is independent. From inside a folder:

```bash
docker compose up --build        # watch the demo output live, then it exits
# or, detached, then read logs:
docker compose up -d --build
docker compose logs -f app
```

> Use `docker compose up` (no `-d`) if you want to **watch the result scroll by**.
> The `app` container runs the demo once, prints a clearly-labelled report, and exits 0.
> The storage container keeps running (so you can poke it with pgAdmin / mongosh / redis-cli).

Tear down with `docker compose down -v`.

## Language choice

Everything is **Python** for consistency and brevity — the race-condition logic is identical
across stores, so only the *storage primitive* changes between folders. Read the code top-to-bottom;
the interesting comments are right next to the SQL / commands explaining **why** each case is safe
or broken.
