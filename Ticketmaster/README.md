# Ticketmaster - Distributed Systems POC

A minimal ticket booking system demonstrating 4 different concurrency control strategies using DDD architecture.

## Architecture

**Domain Layer**: Anemic entities (Event, Seat)  
**Infrastructure Layer**: EF Core with Fluent API (migrations only), Dapper for queries, Redis, Stripe mock  
**Application Layer**: 4 booking strategies implementing different concurrency patterns  
**API Layer**: Minimal API endpoints with parallel execution

## Concurrency Strategies

1. **No Locking** - Race condition demonstration (both requests may succeed, last write wins)
2. **Pessimistic Locking** - PostgreSQL row-level locking with `FOR UPDATE`
3. **Optimistic Locking** - Status-based atomic updates
4. **RedLock** - Distributed locking with Redis `SET NX`

## Running the System

```bash
docker-compose up --build
```

Access Swagger UI at: `http://localhost:8080/swagger`

## Database

Postgres runs on `localhost:5432`  
Redis runs on `localhost:6379`

Migrations are applied automatically on app startup.

## Testing Endpoints

Each endpoint accepts a `seatId` and spawns 2 parallel booking attempts with different users:

- **POST /book/no-lock** - No concurrency control
- **POST /book/pessimistic** - Row locking
- **POST /book/optimistic** - Atomic status update
- **POST /book/redlock** - Redis distributed lock

### Example Request

```json
{
  "seatId": 1
}
```

The system automatically creates 5 seats on first run (IDs 1-5).

## Expected Results

- **No Locking**: May have race condition, last write wins
- **Pessimistic**: One succeeds, one fails (locked)
- **Optimistic**: One succeeds, one fails (status check)
- **RedLock**: One succeeds, one fails (lock not acquired)
