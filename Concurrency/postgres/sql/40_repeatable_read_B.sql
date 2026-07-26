-- =====================================================================
--  SCENARIO 4: REPEATABLE READ  (DB raises serialization_failure)  --  THE FIX
--  This is SESSION B.
-- =====================================================================

-- STEP 3 (B):
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ;

-- STEP 4 (B): B fixes its OWN snapshot - it still sees 'available'.
SELECT id, status FROM seats WHERE id = 1;
--   -> 'available'

-- >>> SWITCH WINDOW: go run A's STEP 5 and STEP 6 <<<

-- STEP 7 (B): B tries to reserve too.
UPDATE seats SET status = 'reserved', reserved_by = 'BOB' WHERE id = 1;
--   ▶ This BLOCKS while A is uncommitted. When A commits, Postgres sees that
--     B is about to overwrite a row that changed AFTER B's snapshot began, so:
--
--     ERROR:  could not serialize access due to concurrent update
--     SQLSTATE: 40001
--
--   The WHOLE transaction B is aborted - nothing it did survives.

-- STEP 8 (B): the tx is already aborted; clean up.
ROLLBACK;
--   WHY IT WORKS: under REPEATABLE READ (and SERIALIZABLE) Postgres refuses the
--   lost update for you. Your APP MUST catch 40001 and retry the whole tx.
--   Trade-off vs optimistic-version: no extra column, but you must handle 40001.

SELECT id, status, reserved_by FROM seats WHERE id = 1;   -- -> ALICE
