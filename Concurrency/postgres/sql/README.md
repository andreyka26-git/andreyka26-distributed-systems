# Step-through SQL for pgAdmin (two query windows)

These files let you *feel* concurrency by hand: open **two** query windows in pgAdmin
(call them **Session A** and **Session B**), and run the numbered `STEP` blocks in the
order written, switching windows when the comment tells you to. You'll literally watch
one session **hang** (blocked on a lock) or **fail** (serialization error).

## Connect pgAdmin
After `docker compose up -d` in the `postgres/` folder:

| field    | value       |
|----------|-------------|
| host     | `localhost` |
| port     | `5433`      |
| database | `demo`      |
| user     | `postgres`  |
| password | `postgres`  |

(You don't even need Docker for this - point pgAdmin at any Postgres and run `00_setup.sql` first.)

## The four scenarios

| Files                                                        | Shows                                   |
|--------------------------------------------------------------|-----------------------------------------|
| `10_lost_update_A.sql` + `10_lost_update_B.sql`              | ❌ the BUG: double booking               |
| `20_pessimistic_A.sql` + `20_pessimistic_B.sql`              | ✅ `FOR UPDATE` - B **blocks** then bails |
| `30_optimistic_A.sql` + `30_optimistic_B.sql`                | ✅ version guard - B gets `UPDATE 0`      |
| `40_repeatable_read_A.sql` + `40_repeatable_read_B.sql`      | ✅ B aborts with `40001` serialization   |

Workflow for each: run `99_reset.sql`, then `..._A.sql` STEP 1–2, then `..._B.sql`,
following the `>>> SWITCH WINDOW <<<` arrows. Reset again before the next scenario.

> Tip: keep `99_reset.sql` in a third window so you can re-arm seat #1 quickly.
