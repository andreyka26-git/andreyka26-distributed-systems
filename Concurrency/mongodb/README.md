# MongoDB - optimistic & "pessimistic" (transactions)

```bash
docker compose up --build
```

## What it shows

| # | Scenario | Mechanism | Result |
|---|----------|-----------|--------|
| 1 | Lost update | read doc, then `update_one({_id:1}, ...)` ignoring status | ❌ double booking |
| 2 | Optimistic (status) | `update_one({_id:1, status:'available'}, ...)` - atomic match-and-set | ✅ one winner |
| 3 | Optimistic (version) | `update_one({_id:1, version:v}, {$inc:{version:1}})` | ✅ one winner |
| 4 | Transaction conflict | two txns write the same doc → `WriteConflict` | ✅ one commits, one aborts |

## Does MongoDB have pessimistic locking?

**Not really.** There's no `SELECT ... FOR UPDATE`. The honest options are:

- **Optimistic** is the idiomatic answer: a single-document update is **atomic**, so put
  your precondition *in the filter* (`status:'available'` or a `version`). The loser simply
  matches zero documents. This is what scenarios 2 & 3 do, and what you'd ship.
- **Transactions** (scenario 4) give document-level locking *within* a transaction via
  WiredTiger, but the loser is **aborted with a `WriteConflict`** (a `TransientTransactionError`)
  rather than *blocked* - so it's conflict detection (optimistic flavored), and you must retry.

> Rule of thumb on MongoDB: reach for the **atomic conditional update** first. Only use
> multi-document transactions when one logical change spans several documents.

Manual poke after it's up:
```bash
docker compose exec mongo mongosh "mongodb://localhost:27017/?replicaSet=rs0" \
  --eval 'db.getSiblingDB("demo").seats.find().pretty()'
```
