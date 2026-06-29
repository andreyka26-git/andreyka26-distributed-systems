-- Runs automatically on first boot of the Postgres container
-- (mounted into /docker-entrypoint-initdb.d). Also safe to run by hand.
--
-- The `seats` table backs both lock_seat strategies:
--   * pessimistic -> SELECT ... FOR UPDATE locks this row
--   * optimistic  -> the `version` column is the compare-and-swap guard

CREATE TABLE IF NOT EXISTS seats (
    id           INT  PRIMARY KEY,
    status       TEXT NOT NULL DEFAULT 'available',  -- 'available' | 'reserved'
    reserved_by  TEXT,
    version      INT  NOT NULL DEFAULT 0             -- bumped on every successful reservation
);

-- Seed seat #1 in a clean 'available' state (idempotent).
INSERT INTO seats (id, status, reserved_by, version)
VALUES (1, 'available', NULL, 0)
ON CONFLICT (id) DO UPDATE
    SET status = 'available', reserved_by = NULL, version = 0;
