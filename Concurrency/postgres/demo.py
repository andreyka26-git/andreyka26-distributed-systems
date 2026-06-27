"""
PostgreSQL concurrency demo: two threads fight to reserve seat #1.

Run order of scenarios (each resets the seat to 'available' first):
  1. LOST UPDATE      -> the BUG. READ COMMITTED + read-then-write, no guard.
  2. OPTIMISTIC       -> FIX. version column + conditional UPDATE.
  3. PESSIMISTIC      -> FIX. SELECT ... FOR UPDATE (row lock).
  4. REPEATABLE READ  -> FIX. DB itself raises 40001 serialization_failure.

The SAME scenarios are available as raw .sql files in ./sql/ so you can step
through them by hand in pgAdmin (two query windows). See sql/README.md.
"""
import os
import sys
import time
import threading
import psycopg2

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass
import psycopg2.extensions
import psycopg2.errors

DSN = dict(
    host=os.getenv("PG_HOST", "localhost"),
    port=os.getenv("PG_PORT", "5432"),
    dbname=os.getenv("PG_DB", "demo"),
    user=os.getenv("PG_USER", "postgres"),
    password=os.getenv("PG_PASSWORD", "postgres"),
)


def connect():
    return psycopg2.connect(**DSN)


def wait_for_db(retries=30):
    for _ in range(retries):
        try:
            connect().close()
            return
        except psycopg2.OperationalError:
            time.sleep(1)
    raise SystemExit("Postgres never became reachable")


def reset_seat():
    """Put seat #1 back to a clean 'available' state between scenarios."""
    c = connect()
    c.autocommit = True
    cur = c.cursor()
    cur.execute(
        """CREATE TABLE IF NOT EXISTS seats(
               id INT PRIMARY KEY,
               status TEXT NOT NULL DEFAULT 'available',
               reserved_by TEXT,
               version INT NOT NULL DEFAULT 0);"""
    )
    cur.execute(
        """INSERT INTO seats(id, status, reserved_by, version)
           VALUES (1, 'available', NULL, 0)
           ON CONFLICT (id) DO UPDATE
             SET status='available', reserved_by=NULL, version=0;"""
    )
    c.close()


def final_seat():
    c = connect()
    cur = c.cursor()
    cur.execute("SELECT status, reserved_by, version FROM seats WHERE id=1")
    row = cur.fetchone()
    c.close()
    return row


def header(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def report(winners, row):
    print(f"  -> winners (threads that believed they reserved it): {winners}")
    print(f"  -> final row: status={row[0]!r} reserved_by={row[1]!r} version={row[2]}")
    if len(winners) == 1:
        print("  ✅ CORRECT: exactly one customer got the seat.")
    else:
        print(f"  ❌ DOUBLE BOOKING: {len(winners)} customers were told 'you got it'.")


# --------------------------------------------------------------------------- #
# 1. LOST UPDATE  — THE BUG
# --------------------------------------------------------------------------- #
def scenario_lost_update():
    header("1. LOST UPDATE  (READ COMMITTED, read-then-write, NO guard)  -- THE BUG")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        c = connect()
        c.set_isolation_level(psycopg2.extensions.ISOLATION_LEVEL_READ_COMMITTED)
        cur = c.cursor()
        # STEP 1: both read the seat. READ COMMITTED gives each its own snapshot.
        cur.execute("SELECT status FROM seats WHERE id=1")
        status = cur.fetchone()[0]
        barrier.wait()  # force BOTH to read 'available' BEFORE either writes
        # STEP 2: app logic. The UPDATE does NOT re-check what we read above,
        #         so both threads happily overwrite. This is the lost update.
        if status == "available":
            cur.execute(
                "UPDATE seats SET status='reserved', reserved_by=%s WHERE id=1",
                (name,),
            )
            winners.append(name)
        c.commit()
        c.close()

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: nothing tied the UPDATE to the value we read. The 2nd UPDATE just")
    print("       blocks on the row lock, waits for the 1st commit, then overwrites it.")


# --------------------------------------------------------------------------- #
# 2. OPTIMISTIC  — FIX via version column
# --------------------------------------------------------------------------- #
def scenario_optimistic():
    header("2. OPTIMISTIC  (version column, conditional UPDATE)  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        c = connect()
        cur = c.cursor()
        # Read the version we are betting on.
        cur.execute("SELECT version FROM seats WHERE id=1")
        version = cur.fetchone()[0]
        barrier.wait()
        # The guard `AND version = <what we read>` is the whole trick:
        # the FIRST committer bumps version 0 -> 1. The loser's UPDATE then
        # matches 0 rows (READ COMMITTED re-checks the WHERE after the block).
        cur.execute(
            """UPDATE seats
                  SET status='reserved', reserved_by=%s, version=version+1
                WHERE id=1 AND version=%s""",
            (name, version),
        )
        if cur.rowcount == 1:
            winners.append(name)  # rowcount==1 -> WE WON
        # rowcount==0 -> someone else moved the version -> we'd retry in real code
        c.commit()
        c.close()

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: the loser's `WHERE version=0` no longer matches (version is now 1)")
    print("       -> 'UPDATE 0' rows -> app sees the conflict and retries/fails.")


# --------------------------------------------------------------------------- #
# 3. PESSIMISTIC  — FIX via SELECT ... FOR UPDATE
# --------------------------------------------------------------------------- #
def scenario_pessimistic():
    header("3. PESSIMISTIC  (SELECT ... FOR UPDATE row lock)  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        c = connect()
        cur = c.cursor()
        barrier.wait()
        # FOR UPDATE locks the row. The SECOND thread BLOCKS here until the
        # first one commits. Access is fully serialized -> no race window.
        cur.execute("SELECT status FROM seats WHERE id=1 FOR UPDATE")
        status = cur.fetchone()[0]
        if status == "available":
            cur.execute(
                "UPDATE seats SET status='reserved', reserved_by=%s WHERE id=1",
                (name,),
            )
            winners.append(name)
        # else: by the time we got the lock it was already 'reserved' -> refuse.
        c.commit()  # releasing the lock lets the other thread proceed
        c.close()

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: the 2nd thread can't even SELECT the row until the 1st commits,")
    print("       and by then it reads 'reserved' and backs off.")


# --------------------------------------------------------------------------- #
# 4. REPEATABLE READ  — FIX, DB raises serialization_failure (40001)
# --------------------------------------------------------------------------- #
def scenario_repeatable_read():
    header("4. REPEATABLE READ  (DB raises 40001 on conflicting write)  -- FIX")
    reset_seat()
    winners = []
    outcomes = []
    barrier = threading.Barrier(2)

    def worker(name):
        c = connect()
        c.set_isolation_level(psycopg2.extensions.ISOLATION_LEVEL_REPEATABLE_READ)
        cur = c.cursor()
        cur.execute("SELECT status FROM seats WHERE id=1")
        status = cur.fetchone()[0]
        barrier.wait()
        try:
            if status == "available":
                cur.execute(
                    "UPDATE seats SET status='reserved', reserved_by=%s WHERE id=1",
                    (name,),
                )
            c.commit()
            winners.append(name)
            outcomes.append(f"{name}: committed")
        except psycopg2.errors.SerializationFailure:
            # Postgres detected we wrote a row another tx changed under our snapshot.
            c.rollback()
            outcomes.append(f"{name}: 40001 serialization_failure -> must retry")
        c.close()

    run_two(worker)
    print(f"  outcomes: {outcomes}")
    report(winners, final_seat())
    print("  WHY: under REPEATABLE READ the loser's snapshot is stale, so Postgres")
    print("       aborts it with SQLSTATE 40001 instead of allowing a lost update.")


def run_two(worker):
    """Start two threads, slightly staggered so one tends to grab the lock first."""
    t1 = threading.Thread(target=worker, args=("ALICE",))
    t2 = threading.Thread(target=worker, args=("BOB",))
    t1.start()
    time.sleep(0.05)
    t2.start()
    t1.join()
    t2.join()


if __name__ == "__main__":
    wait_for_db()
    scenario_lost_update()
    scenario_optimistic()
    scenario_pessimistic()
    scenario_repeatable_read()
    print("\nDone. The `postgres` container is still up — try the ./sql/ files in pgAdmin.\n")
