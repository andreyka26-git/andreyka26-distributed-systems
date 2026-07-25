-- =====================================================================
--  SCENARIO 2: PESSIMISTIC  (SELECT ... FOR UPDATE)   --  THE FIX
--  This is SESSION A.  Run `99_reset.sql` first.
--  Works in pgAdmin (pure SQL, no psql meta-commands).
-- =====================================================================

BEGIN;

-- Lock the row so nobody else can read-for-update until we COMMIT/ROLLBACK.
SELECT id, status FROM seats WHERE id = 1 FOR UPDATE;

-- The decision the application code would make on the locked row.
-- (See the Messages tab in pgAdmin for the RAISE NOTICE output.)
DO $$
DECLARE
    seat_status text;
BEGIN
    SELECT status INTO seat_status FROM seats WHERE id = 1 FOR UPDATE;

    IF seat_status = 'available' THEN
        UPDATE seats SET status = 'reserved', reserved_by = 'ALICE' WHERE id = 1;
        RAISE NOTICE '>> Seat was free -- reserved by ALICE.';
    ELSE
        RAISE NOTICE '>> Seat already reserved -- application rejects, no change.';
    END IF;
END $$;

-- COMMIT is safe either way: on the reject path nothing was updated.
COMMIT;

SELECT id, status, reserved_by FROM seats WHERE id = 1;
