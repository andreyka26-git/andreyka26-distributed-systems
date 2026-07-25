-- =====================================================================
--  SCENARIO 2: PESSIMISTIC  (SELECT ... FOR UPDATE)   --  THE FIX
--  This is SESSION B.
--  Works in pgAdmin (pure SQL, no psql meta-commands).
-- =====================================================================

BEGIN;

-- This SELECT ... FOR UPDATE BLOCKS until session A commits, because A
-- holds the row lock. Once A commits, we read the *fresh* row (reserved).
SELECT id, status FROM seats WHERE id = 1 FOR UPDATE;

-- The decision the application code would make on the locked row.
-- (See the Messages tab in pgAdmin for the RAISE NOTICE output.)
DO $$
DECLARE
    seat_status text;
BEGIN
    SELECT status INTO seat_status FROM seats WHERE id = 1 FOR UPDATE;

    IF seat_status = 'available' THEN
        UPDATE seats SET status = 'reserved', reserved_by = 'BOB' WHERE id = 1;
        RAISE NOTICE '>> Seat was free -- reserved by BOB.';
    ELSE
        RAISE NOTICE '>> Seat already reserved -- application rejects, no change.';
    END IF;
END $$;

-- COMMIT is safe either way: on the reject path nothing was updated.
COMMIT;

SELECT id, status, reserved_by FROM seats WHERE id = 1;
