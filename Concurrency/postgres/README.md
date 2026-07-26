# PostgreSQL - optimistic vs pessimistic vs isolation levels

## Run the automated demo
```bash
docker compose up --build      # watch 4 scenarios run and self-report
```
Expected output (abridged):
```
1. LOST UPDATE ...                ❌ DOUBLE BOOKING: 2 customers were told 'you got it'.
2. OPTIMISTIC ...                 ✅ CORRECT: exactly one customer got the seat.
3. PESSIMISTIC ...                ✅ CORRECT: exactly one customer got the seat.
4. REPEATABLE READ ...            ✅ CORRECT: exactly one customer got the seat.
```

## Step through it by hand in pgAdmin
The Postgres container stays up after the demo. Open **two query windows** and follow
the numbered `.sql` files in [`sql/`](sql/) - you'll watch one session **block** on a lock
or **fail** with `40001`. See [`sql/README.md`](sql/README.md). Connection: `localhost:5433`, db `demo`, `postgres`/`postgres`.

## The three fixes, side by side

| Strategy                  | Statement that makes it safe                                   | Loser experiences |
|---------------------------|----------------------------------------------------------------|-------------------|
| Pessimistic               | `SELECT ... FOR UPDATE` then `UPDATE`                           | **blocks**, then reads 'reserved' & backs off |
| Optimistic (version)      | `UPDATE ... WHERE id=1 AND version=:v`                          | `UPDATE 0` rows → retry |
| Isolation (RR/Serializable)| plain `UPDATE` under `REPEATABLE READ`                         | `ERROR 40001` → retry whole tx |

## Isolation level cheat-sheet (for the seat problem)

| Level            | Lost update via read-then-write? | Notes |
|------------------|----------------------------------|-------|
| `READ COMMITTED` (default) | **Yes, possible** - needs `FOR UPDATE` or a version guard | each statement sees latest committed data |
| `REPEATABLE READ`| **No** - write conflict → `40001` | snapshot fixed at first query; cheap, but you must retry |
| `SERIALIZABLE`   | **No** - `40001` | strongest; also catches phantom/skew cases the seat demo doesn't need |

> Pessimistic = "I'll wait my turn." Optimistic = "I'll try and apologize if I lose."
> Pick pessimistic under high contention on the same row; optimistic when conflicts are rare.
