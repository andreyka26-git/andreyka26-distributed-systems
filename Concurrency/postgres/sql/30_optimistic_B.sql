-- =====================================================================
--  SCENARIO 3: OPTIMISTIC  (version column, conditional UPDATE)  --  THE FIX
--  This is SESSION B.
-- =====================================================================

-- STEP 3 (B):
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- STEP 4 (B): B reads the same version.
SELECT id, status, version FROM seats WHERE id = 1;
--   -> version = 0  (same bet as A)

-- >>> SWITCH WINDOW: go run A's STEP 5 and STEP 6 <<<

-- STEP 7 (B): B tries the same conditional update guarded by version = 0.
UPDATE seats
   SET status = 'reserved', reserved_by = 'BOB', version = version + 1
 WHERE id = 1 AND version = 0;
--   ▶ While A's UPDATE is uncommitted this BLOCKS on the row lock.
--   ▶ When A commits, READ COMMITTED RE-EVALUATES the WHERE against the new row:
--     version is now 1, so `version = 0` no longer matches.
--   pgAdmin message: "UPDATE 0"  -> B changed NOTHING. B lost the optimistic bet.

-- STEP 8 (B):
COMMIT;
--   WHY IT WORKS: the `AND version = 0` guard makes the write conditional on the
--   exact row we read. Loser sees 0 rows affected -> app retries or returns
--   "seat taken". No lock was held while we "thought" -> high concurrency.

SELECT id, status, reserved_by, version FROM seats WHERE id = 1;
--   -> ALICE, version = 1.
