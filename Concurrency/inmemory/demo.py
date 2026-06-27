"""
In-memory concurrency demo: N threads fight to reserve seat #1, where the
"database" is just a Python dict living in this process.

Scenarios:
  1. NO LOCK      -> the BUG. read-modify-write with a race window.
  2. PESSIMISTIC  -> FIX. threading.Lock around the critical section (others WAIT).
  3. OPTIMISTIC   -> FIX. version + compare-and-swap retry loop (losers RETRY).
  4. ATOMIC       -> FIX. a single indivisible claim (queue.Queue token).

We use MORE than two threads and a tiny sleep to widen the race window so the
bug reproduces reliably despite Python's GIL.
"""
import sys
import time
import threading
import queue

# Make ✅/❌ printable on the Windows console (cp1252) as well as in Docker (utf-8).
try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

N = 8  # number of competing "customers"


def header(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def report(winners):
    print(f"  -> winners ({len(winners)}): {winners}")
    if len(winners) == 1:
        print("  ✅ CORRECT: exactly one customer got the seat.")
    else:
        print(f"  ❌ DOUBLE BOOKING: {len(winners)} customers thought they won.")


def run(worker):
    start = threading.Barrier(N)   # release all threads at the same instant
    winners = []
    lock_for_results = threading.Lock()

    def wrapped(name):
        start.wait()
        if worker(name):
            with lock_for_results:      # only protects the test's bookkeeping
                winners.append(name)

    threads = [threading.Thread(target=wrapped, args=(f"C{i}",)) for i in range(N)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    report(winners)


# --------------------------------------------------------------------------- #
# 1. NO LOCK — THE BUG
# --------------------------------------------------------------------------- #
def scenario_no_lock():
    header("1. NO LOCK  (read-modify-write, unguarded)  -- THE BUG")
    seat = {"status": "available", "reserved_by": None}

    def worker(name):
        if seat["status"] == "available":   # CHECK
            time.sleep(0.001)               # <-- race window: others also pass CHECK
            seat["status"] = "reserved"     # ACT
            seat["reserved_by"] = name
            return True
        return False

    run(worker)
    print("  WHY: check ('available?') and act ('reserve') are separate steps. Every")
    print("       thread passes the check before anyone acts -> all 'win'.")


# --------------------------------------------------------------------------- #
# 2. PESSIMISTIC — threading.Lock
# --------------------------------------------------------------------------- #
def scenario_pessimistic():
    header("2. PESSIMISTIC  (threading.Lock — others block)  -- FIX")
    seat = {"status": "available", "reserved_by": None}
    lock = threading.Lock()

    def worker(name):
        with lock:                          # only ONE thread is inside at a time;
            if seat["status"] == "available":  # the rest BLOCK here until released
                time.sleep(0.001)
                seat["status"] = "reserved"
                seat["reserved_by"] = name
                return True
            return False

    run(worker)
    print("  WHY: the lock makes check+act mutually exclusive. Whoever enters first")
    print("       reserves; everyone else waits, then sees 'reserved' and backs off.")
    print("       (Pessimistic = take the lock BEFORE touching the data.)")


# --------------------------------------------------------------------------- #
# 3. OPTIMISTIC — version + compare-and-swap retry loop
# --------------------------------------------------------------------------- #
def scenario_optimistic():
    header("3. OPTIMISTIC  (version + compare-and-swap retry)  -- FIX")
    # The shared cell. `cas_lock` stands in for a HARDWARE compare-and-swap
    # instruction: the swap itself is atomic, but no lock is held while a thread
    # is "thinking" / preparing its update.
    seat = {"status": "available", "reserved_by": None, "version": 0}
    cas_lock = threading.Lock()

    def compare_and_swap(expected_version, new_status, who):
        """Atomically: if version still == expected, apply the change & bump version."""
        with cas_lock:
            if seat["version"] != expected_version:
                return False                # someone changed it -> our bet is stale
            seat["status"] = new_status
            seat["reserved_by"] = who
            seat["version"] += 1
            return True

    def worker(name):
        while True:                         # optimistic loop: try, fail, re-read, retry
            v = seat["version"]             # read version (no lock held)
            if seat["status"] != "available":
                return False                # already taken, give up
            time.sleep(0.001)               # think... (others may swap meanwhile)
            if compare_and_swap(v, "reserved", name):
                return True                 # our version was still current -> we won
            # else: lost the CAS, loop around and re-read the latest state

    run(worker)
    print("  WHY: nobody holds a lock while deciding. The commit is a CAS that only")
    print("       succeeds if the version is unchanged. Losers retry against fresh")
    print("       state and discover it's taken. Great when contention is low.")


# --------------------------------------------------------------------------- #
# 4. ATOMIC — a single indivisible claim (Queue token)
# --------------------------------------------------------------------------- #
def scenario_atomic():
    header("4. ATOMIC  (single indivisible claim via queue.Queue)  -- FIX")
    # Put exactly ONE token in the queue. Reserving == taking the token.
    # Queue.get_nowait() is a single atomic operation: only one thread can pull
    # the one token; everyone else immediately gets Empty.
    seat_token = queue.Queue(maxsize=1)
    seat_token.put("seat:1")

    def worker(name):
        try:
            seat_token.get_nowait()         # atomic claim — no check-then-act gap
            return True
        except queue.Empty:
            return False                    # token already gone -> seat taken

    run(worker)
    print("  WHY: there is no separate check. 'Take the one token' is itself the test;")
    print("       it can't be split, so it can't race. This is the spirit of an atomic")
    print("       CPU instruction / Redis SETNX / DynamoDB conditional write.")


if __name__ == "__main__":
    scenario_no_lock()
    scenario_pessimistic()
    scenario_optimistic()
    scenario_atomic()
    print()
