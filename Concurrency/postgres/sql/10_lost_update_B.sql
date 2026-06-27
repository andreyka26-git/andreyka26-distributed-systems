-- =====================================================================
--  SCENARIO 1: LOST UPDATE  (READ COMMITTED)   --  THE BUG
--  This is SESSION B.  Follow the STEP numbers across both windows.
-- =====================================================================

-- STEP 3 (B): start the transaction
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- STEP 4 (B): B also reads BEFORE A has committed -> also sees 'available'.
SELECT id, status FROM seats WHERE id = 1;
--   -> status = 'available'   (both A and B now believe the seat is free)

-- >>> SWITCH WINDOW: go run STEP 5 and STEP 6 in SESSION A <<<

-- STEP 7 (B): B reserves for BOB.
--   While A's UPDATE is uncommitted, THIS STATEMENT BLOCKS on the row lock.
--   The instant A commits (STEP 6), it unblocks and overwrites ALICE.
UPDATE seats SET status = 'reserved', reserved_by = 'BOB' WHERE id = 1;

-- STEP 8 (B): commit
COMMIT;
--   WHY IT'S BROKEN: READ COMMITTED does not protect a read-then-write.
--   The UPDATE re-reads the latest row (now ALICE's) and blindly overwrites it,
--   because nothing in the statement says "...only if it's still what I read".
