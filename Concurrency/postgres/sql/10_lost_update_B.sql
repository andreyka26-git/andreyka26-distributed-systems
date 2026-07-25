-- =====================================================================
--  SCENARIO 1: LOST UPDATE  (READ COMMITTED)   --  THE BUG
--  This is SESSION B.  Follow the STEP numbers across both windows.
-- =====================================================================

BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

SELECT id, status FROM seats WHERE id = 1;
UPDATE seats SET status = 'reserved', reserved_by = 'BOB' WHERE id = 1;

COMMIT;
