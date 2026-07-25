-- 1
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;

SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

-- 3
UPDATE public.seats
   SET reserved_by = 'ALICE',
   	   status      = 'reserved',
       version     = version + 1
 WHERE id = 1;

-- 4
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;
 
-- 7
COMMIT;

-- 9
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

-- 12
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

