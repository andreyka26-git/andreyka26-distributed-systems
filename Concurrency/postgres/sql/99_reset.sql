-- Run this between scenarios to put seat #1 back to 'available'.
UPDATE seats SET status = 'available', reserved_by = NULL, version = 0 WHERE id = 1;
SELECT id, status, reserved_by, version FROM seats WHERE id = 1;
