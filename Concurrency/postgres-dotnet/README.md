# PostgreSQL + .NET (Dapper) — `lock_seat`: pessimistic vs optimistic

A production-style .NET 8 take on the seat-reservation race. One operation, `lock_seat`,
implemented two ways against Postgres via **Dapper**:

| Method                          | Strategy        | How it stays safe                                            | Loser experiences |
|---------------------------------|-----------------|-------------------------------------------------------------|-------------------|
| `LockSeatPessimisticAsync`      | **Pessimistic** | `SELECT ... FOR UPDATE` row lock, then `UPDATE`             | **blocks** on the lock, then reads `reserved` and backs off |
| `LockSeatOptimisticAsync`       | **Optimistic**  | versioned compare-and-swap: `UPDATE ... WHERE version = @v` | `UPDATE 0` rows → re-read & retry, then backs off |

See [src/SeatLocker.cs](src/SeatLocker.cs) for both implementations and the inline comments
explaining *why* each is safe. [src/Program.cs](src/Program.cs) runs a two-customer race for
each strategy and self-reports.

## Run the demo

```bash
docker compose up --build
```

Expected output (abridged):

```
PESSIMISTIC ...   CORRECT: exactly one customer got the seat.
OPTIMISTIC  ...   CORRECT: exactly one customer got the seat.
```

The `app` container runs once and exits; the `postgres` container stays up so you can poke it.

## Run locally (without containerizing the app)

Start just the database, then run the .NET app on the host:

```bash
docker compose up -d postgres
dotnet run --project src
```

The app defaults to `Host=localhost;Port=5434;Database=seats;...`. Override with the
`POSTGRES_CONNECTION` environment variable.

## Naming / ports (parallel-safe)

This stack is namespaced so it can run **at the same time** as the sibling demos:

| Thing            | This folder (`postgres-dotnet`) | Sibling (`postgres`) |
|------------------|---------------------------------|----------------------|
| compose project  | `concurrency-postgres-dotnet`   | (default)            |
| host port        | **5434**                        | 5433                 |
| database         | `seats`                         | `demo`               |
| container names  | `concurrency-pg-dotnet`, `concurrency-app-dotnet` | (default) |

Schema is created and seeded automatically by [sql/00_setup.sql](sql/00_setup.sql) on first
container boot (mounted into `/docker-entrypoint-initdb.d`).

Tear down with `docker compose down -v`.

> **Pessimistic** = "I'll wait my turn" — best under high contention on the same row.
> **Optimistic** = "I'll try and apologize if I lose" — best when conflicts are rare.
