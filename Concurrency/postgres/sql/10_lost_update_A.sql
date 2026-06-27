-- =====================================================================
--  SCENARIO 1: LOST UPDATE  (READ COMMITTED)   --  THE BUG
--  This is SESSION A.  Open 10_lost_update_B.sql in a 2nd window = SESSION B.
--  Run `99_reset.sql` first.
-- =====================================================================

-- STEP 1 (A): start the transaction
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

-- STEP 2 (A): read the seat. App sees 'available' and DECIDES to take it.
SELECT id, status FROM seats WHERE id = 1;
--   -> status = 'available'

-- >>> SWITCH WINDOW: go run STEP 3 and STEP 4 in SESSION B now <<<

-- STEP 5 (A): we reserve it for ALICE. We never re-checked the value from STEP 2.
UPDATE seats SET status = 'reserved', reserved_by = 'ALICE' WHERE id = 1;

-- STEP 6 (A): commit. This releases the row lock that B is now waiting on.
COMMIT;

-- >>> SWITCH WINDOW: B's blocked UPDATE (STEP 7) will now unblock and OVERWRITE us <<<

-- STEP 9 (A): see the damage
SELECT id, status, reserved_by FROM seats WHERE id = 1;
--   -> reserved_by = 'BOB'.  ALICE's reservation was silently LOST,
--      yet BOTH customers were told "you got the seat". Double booking.
