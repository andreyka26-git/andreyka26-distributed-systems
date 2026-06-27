-- =====================================================================
--  SCENARIO 3: OPTIMISTIC  (version column, conditional UPDATE)  --  THE FIX
--  This is SESSION A.  Run `99_reset.sql` first (sets version = 0).
--  No locks taken up front. We bet that the row hasn't changed.
-- =====================================================================

-- STEP 1 (A):
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- STEP 2 (A): read the version we are betting on.
SELECT id, status, version FROM seats WHERE id = 1;
--   -> version = 0

-- >>> SWITCH WINDOW: run STEP 3 and STEP 4 in SESSION B <<<

-- STEP 5 (A): conditional update — only succeeds if version is still 0.
UPDATE seats
   SET status = 'reserved', reserved_by = 'ALICE', version = version + 1
 WHERE id = 1 AND version = 0;          -- 0 = the version we read in STEP 2
--   pgAdmin message: "UPDATE 1"  -> WE WON. version is now 1.

-- STEP 6 (A):
COMMIT;

-- >>> SWITCH WINDOW: B's STEP 7 UPDATE will report "UPDATE 0" — it lost. <<<
