-- =====================================================================
--  SCENARIO 4: REPEATABLE READ  (DB raises serialization_failure)  --  THE FIX
--  This is SESSION A.  Run `99_reset.sql` first.
--  Here we DON'T add a version guard — we let the isolation level catch it.
-- =====================================================================

-- STEP 1 (A):
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ;

-- STEP 2 (A): this fixes our snapshot of the database for the whole tx.
SELECT id, status FROM seats WHERE id = 1;
--   -> 'available'

-- >>> SWITCH WINDOW: run STEP 3 and STEP 4 in SESSION B <<<

-- STEP 5 (A): plain update (no version guard).
UPDATE seats SET status = 'reserved', reserved_by = 'ALICE' WHERE id = 1;

-- STEP 6 (A): commit -> A wins cleanly.
COMMIT;

-- >>> SWITCH WINDOW: B's update/commit will ERROR with 40001. <<<
