# DynamoDB - optimistic only (by design)

```bash
docker compose up --build
```
Uses [`amazon/dynamodb-local`] in-memory, so no AWS account/credentials needed.

## What it shows

| # | Scenario | Mechanism | Result |
|---|----------|-----------|--------|
| 1 | Lost update | `get_item` then unconditional `put_item` | ❌ double booking |
| 2 | Optimistic (status) | `update_item` + `ConditionExpression "#s = available"` | ✅ one winner |
| 3 | Optimistic (version) | `update_item` + `ConditionExpression "version = :ov"` | ✅ one winner |

## Does DynamoDB have pessimistic locking?

**No.** DynamoDB has **no blocking locks** - no `SELECT ... FOR UPDATE`, nothing waits.
Its one and only native concurrency primitive is the **conditional write**
(`ConditionExpression`), which is pure **optimistic** concurrency. The loser gets a
`ConditionalCheckFailedException` and retries.

- `@DynamoDBVersionAttribute` in the AWS SDK mappers is exactly scenario 3 under the hood.
- `TransactWriteItems` (ACID transactions, up to 100 items) also relies on conditions and
  is **still optimistic** - a conflicting concurrent write makes the whole transaction fail
  with `TransactionCanceledException`. It does not block.

> If you genuinely need pessimistic, app-level "leases" are the usual workaround: write a
> `lockedUntil` timestamp with a conditional write and have others honor it - but that's you
> building optimistic locking into a lease, not the database blocking anyone.
