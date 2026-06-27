-- Run ONCE to create + seed the demo table.
-- (Docker also runs this automatically on first boot via /docker-entrypoint-initdb.d.)
CREATE TABLE IF NOT EXISTS seats (
    id           INT  PRIMARY KEY,
    status       TEXT NOT NULL DEFAULT 'available',  -- 'available' | 'reserved'
    reserved_by  TEXT,
    version      INT  NOT NULL DEFAULT 0              -- used by the OPTIMISTIC demo
);

INSERT INTO seats (id, status, reserved_by, version)
VALUES (1, 'available', NULL, 0)
ON CONFLICT (id) DO UPDATE
    SET status = 'available', reserved_by = NULL, version = 0;
