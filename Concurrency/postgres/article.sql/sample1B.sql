
-- 2
BEGIN TRANSACTION ISOLATION LEVEL READ COMMITTED;


SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

-- 5
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

-- 6
UPDATE public.seats
   SET reserved_by = 'BOB',
       status      = 'reserved',
       version     = version + 1
 WHERE id = 1;

 -- 8
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;

-- 10
COMMIT;

-- 11
SELECT id, status, reserved_by, version FROM public.seats WHERE id = 1;