-- =====================================================================
--  SCENARIO 1: LOST UPDATE  (READ COMMITTED)   --  THE BUG
--  This is SESSION A.  Open 10_lost_update_B.sql in a 2nd window = SESSION B.
--  Run `99_reset.sql` first.
-- =====================================================================

BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

SELECT id, status FROM seats WHERE id = 1;
UPDATE seats SET status = 'reserved', reserved_by = 'ALICE' WHERE id = 1;

COMMIT;


