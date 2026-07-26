"""
MongoDB concurrency demo: two threads fight to reserve seat #1.

Scenarios:
  1. LOST UPDATE   -> the BUG. read doc, then unconditionally replace status.
  2. OPTIMISTIC    -> FIX. single-document atomic conditional update
                     updateOne({_id, status:'available'}, {$set: reserved}).
  3. OPTIMISTIC v  -> FIX. explicit version field (same idea, generalises to
                     multi-field edits).
  4. PESSIMISTIC   -> transactions. Two txns touching the same doc -> the loser
                     gets a WriteConflict / TransientTransactionError and aborts.
                     (Mongo has no SELECT ... FOR UPDATE; transactions + retry is
                     the closest "pessimistic-feeling" tool, but it's still
                     conflict-detection, i.e. optimistic under the hood.)
"""
import os
import sys
import time
import threading
from pymongo import MongoClient

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass
from pymongo.errors import ConnectionFailure, PyMongoError

URI = os.getenv("MONGO_URI", "mongodb://localhost:27018/?replicaSet=rs0")


def get_client():
    return MongoClient(URI, serverSelectionTimeoutMS=3000)


def wait_for_primary(retries=60):
    for _ in range(retries):
        try:
            c = get_client()
            c.admin.command("ping")
            # ensure a primary has actually been elected (replica set ready)
            if c.admin.command("hello").get("isWritablePrimary"):
                return c
        except (ConnectionFailure, PyMongoError):
            pass
        time.sleep(1)
    raise SystemExit("MongoDB primary never became ready")


client = None
seats = None


def reset_seat():
    seats.replace_one(
        {"_id": 1},
        {"_id": 1, "status": "available", "reserved_by": None, "version": 0},
        upsert=True,
    )


def final_seat():
    d = seats.find_one({"_id": 1})
    return d["status"], d["reserved_by"], d["version"]


def header(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def report(winners, row):
    print(f"  -> winners: {winners}")
    print(f"  -> final doc: status={row[0]!r} reserved_by={row[1]!r} version={row[2]}")
    if len(winners) == 1:
        print("  ✅ CORRECT: exactly one customer got the seat.")
    else:
        print(f"  ❌ DOUBLE BOOKING: {len(winners)} customers thought they won.")


def run_two(worker):
    t1 = threading.Thread(target=worker, args=("ALICE",))
    t2 = threading.Thread(target=worker, args=("BOB",))
    t1.start()
    t2.start()
    t1.join()
    t2.join()


# --------------------------------------------------------------------------- #
# 1. LOST UPDATE - THE BUG
# --------------------------------------------------------------------------- #
def scenario_lost_update():
    header("1. LOST UPDATE  (read, then unconditional write)  -- THE BUG")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        doc = seats.find_one({"_id": 1})         # read
        barrier.wait()                           # both read 'available' first
        if doc["status"] == "available":
            # BUG: the filter is just {_id:1}; it ignores the status we read.
            # Both writes match and the last writer wins -> lost update.
            seats.update_one(
                {"_id": 1},
                {"$set": {"status": "reserved", "reserved_by": name}},
            )
            winners.append(name)

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: the update filter didn't include `status:'available'`, so the")
    print("       check-then-act was not atomic. Two writers both 'succeeded'.")


# --------------------------------------------------------------------------- #
# 2. OPTIMISTIC - atomic conditional update (the idiomatic Mongo fix)
# --------------------------------------------------------------------------- #
def scenario_optimistic_status():
    header("2. OPTIMISTIC  (atomic conditional updateOne on status)  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        barrier.wait()
        # The condition is INSIDE the filter -> the match-and-set is a single
        # atomic operation on one document. Only the first writer matches a doc
        # whose status is still 'available'; the second matches 0 docs.
        res = seats.update_one(
            {"_id": 1, "status": "available"},
            {"$set": {"status": "reserved", "reserved_by": name}},
        )
        if res.modified_count == 1:
            winners.append(name)   # we matched the 'available' doc -> we won

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: single-document updates are atomic in MongoDB. Folding the")
    print("       precondition into the filter removes the race window entirely.")


# --------------------------------------------------------------------------- #
# 3. OPTIMISTIC - explicit version field
# --------------------------------------------------------------------------- #
def scenario_optimistic_version():
    header("3. OPTIMISTIC  (explicit version field)  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        doc = seats.find_one({"_id": 1})
        version = doc["version"]
        barrier.wait()
        res = seats.update_one(
            {"_id": 1, "version": version},   # bet that nobody bumped version
            {"$set": {"status": "reserved", "reserved_by": name},
             "$inc": {"version": 1}},
        )
        if res.modified_count == 1:
            winners.append(name)

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: same as #2 but with a version counter - the general pattern when")
    print("       you edit many fields and can't express the guard as one field.")


# --------------------------------------------------------------------------- #
# 4. PESSIMISTIC-ish - multi-document transaction -> WriteConflict
# --------------------------------------------------------------------------- #
def scenario_transaction_conflict():
    header("4. TRANSACTION  (two txns on same doc -> WriteConflict)  -- FIX")
    reset_seat()
    winners = []
    outcomes = []
    barrier = threading.Barrier(2)

    def worker(name):
        with client.start_session() as session:
            barrier.wait()
            try:
                with session.start_transaction():
                    doc = seats.find_one({"_id": 1}, session=session)
                    time.sleep(0.1)  # widen the window so both are inside the txn
                    if doc["status"] == "available":
                        seats.update_one(
                            {"_id": 1},
                            {"$set": {"status": "reserved", "reserved_by": name}},
                            session=session,
                        )
                # commit happens on exiting the `with` block
                winners.append(name)
                outcomes.append(f"{name}: committed")
            except PyMongoError as e:
                # The 2nd writer touching the same doc is aborted with a
                # WriteConflict (a TransientTransactionError) -> retry in real code.
                label = "WriteConflict/Transient" if e.has_error_label(
                    "TransientTransactionError") else type(e).__name__
                outcomes.append(f"{name}: {label} -> abort & retry")

    run_two(worker)
    print(f"  outcomes: {outcomes}")
    report(winners, final_seat())
    print("  WHY: WiredTiger gives document-level locking inside transactions.")
    print("       The loser can't commit a write to a doc another txn modified")
    print("       -> WriteConflict. NOTE: this is still optimistic detection;")
    print("       MongoDB has no real SELECT ... FOR UPDATE blocking lock.")


if __name__ == "__main__":
    client = wait_for_primary()
    seats = client["demo"]["seats"]
    scenario_lost_update()
    scenario_optimistic_status()
    scenario_optimistic_version()
    scenario_transaction_conflict()
    print("\nDone. Mongo is still up on localhost:27018 (mongosh / Compass).\n")
