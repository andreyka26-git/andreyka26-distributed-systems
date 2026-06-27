-- =====================================================================
--  SCENARIO 2: PESSIMISTIC  (SELECT ... FOR UPDATE)   --  THE FIX
--  This is SESSION A.  Run `99_reset.sql` first.
-- =====================================================================

-- STEP 1 (A):
BEGIN;

-- STEP 2 (A): grab a ROW LOCK on seat #1. Any other FOR UPDATE on this row WAITS.
SELECT id, status FROM seats WHERE id = 1 FOR UPDATE;
--   -> status = 'available'. We hold the lock now.

-- >>> SWITCH WINDOW: run STEP 3 in SESSION B. Notice B *HANGS* (spinner) — it is
--     blocked waiting for our lock. That waiting IS pessimistic concurrency. <<<

-- STEP 4 (A): safely reserve, because nobody else can read-for-update right now.
UPDATE seats SET status = 'reserved', reserved_by = 'ALICE' WHERE id = 1;

-- STEP 5 (A): commit -> releases the lock -> B unblocks.
COMMIT;

-- >>> SWITCH WINDOW: B (STEP 6) now reads 'reserved' and refuses. No double booking. <<<
