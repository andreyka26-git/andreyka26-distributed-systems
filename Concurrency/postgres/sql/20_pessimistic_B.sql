-- =====================================================================
--  SCENARIO 2: PESSIMISTIC  (SELECT ... FOR UPDATE)   --  THE FIX
--  This is SESSION B.
-- =====================================================================

-- STEP 3 (B):
BEGIN;

-- STEP 3 (B, cont.): try to lock the same row.
--   ▶ THIS WILL HANG until SESSION A commits (its STEP 5). Watch the spinner.
SELECT id, status FROM seats WHERE id = 1 FOR UPDATE;
--   After A commits, this UNBLOCKS and returns the FRESH row:
--   -> status = 'reserved', because A took it while we waited.

-- STEP 6 (B): our app logic checks the status it just read.
--   status is 'reserved' (not 'available') -> we must NOT take the seat.
--   (We simply don't UPDATE.) Roll back; the seat stays ALICE's.
ROLLBACK;

SELECT id, status, reserved_by FROM seats WHERE id = 1;
--   -> reserved_by = 'ALICE'. Exactly one winner.
--   WHY IT WORKS: FOR UPDATE serialized access — B literally could not look at
--   the row until A finished, so B saw the committed truth and backed off.
