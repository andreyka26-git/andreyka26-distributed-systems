"""
Redis concurrency demo: two threads fight to reserve seat #1.

Scenarios:
  1. GET-then-SET   -> the BUG. Non-atomic check-then-act.
  2. SET NX (ATOMIC)-> FIX. One round-trip that sets only if absent.
  3. LUA  (ATOMIC)  -> FIX. A whole check-and-set runs as one indivisible script.
  4. WATCH/MULTI    -> FIX. OPTIMISTIC: abort the txn if the key changed.

Redis is single-threaded for command execution, which is what makes individual
commands (and Lua scripts, and MULTI/EXEC) atomic.
"""
import os
import sys
import time
import threading
import redis

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

r = redis.Redis(
    host=os.getenv("REDIS_HOST", "localhost"),
    port=int(os.getenv("REDIS_PORT", "6379")),
    decode_responses=True,
)

SEAT = "seat:1"          # used by string-based scenarios (1, 2)
SEAT_H = "seat:hash:1"   # used by Lua / WATCH scenarios (3, 4)


def wait_for_redis(retries=30):
    for _ in range(retries):
        try:
            r.ping()
            return
        except redis.exceptions.ConnectionError:
            time.sleep(1)
    raise SystemExit("Redis never became reachable")


def header(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def report(winners):
    print(f"  -> winners: {winners}")
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
# 1. GET then SET — THE BUG
# --------------------------------------------------------------------------- #
def scenario_get_then_set():
    header("1. GET-then-SET  (check, then act — two round-trips)  -- THE BUG")
    r.delete(SEAT)
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        taken = r.get(SEAT)            # round-trip #1: is it free?
        barrier.wait()                 # both observe "free" before either writes
        if taken is None:
            # Another client can SET in the gap between GET and SET above.
            r.set(SEAT, name)          # round-trip #2: take it
            winners.append(name)

    run_two(worker)
    report(winners)
    print("  WHY: GET and SET are two separate commands. The gap between them is the")
    print("       race window. Atomicity of each command alone doesn't help here.")


# --------------------------------------------------------------------------- #
# 2. SET NX — ATOMIC
# --------------------------------------------------------------------------- #
def scenario_setnx():
    header("2. SET key val NX  (atomic 'set if not exists')  -- FIX")
    r.delete(SEAT)
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        barrier.wait()
        # nx=True -> the check ("not exists") and the set happen as ONE atomic
        # command. Exactly one client gets True; the other gets None.
        if r.set(SEAT, name, nx=True):
            winners.append(name)
        # Add ex=<seconds> in real systems so a crashed holder's lock expires.

    run_two(worker)
    report(winners)
    print("  WHY: SET NX folds check-and-set into a single atomic command. This is")
    print("       the basis of the simplest Redis lock (and SETNX is the old name).")


# --------------------------------------------------------------------------- #
# 3. LUA — ATOMIC check-and-set script
# --------------------------------------------------------------------------- #
RESERVE_LUA = """
-- KEYS[1] = seat hash, ARGV[1] = customer name
-- Runs atomically: Redis executes the whole script without interleaving others.
if redis.call('HGET', KEYS[1], 'status') == 'available' then
    redis.call('HSET', KEYS[1], 'status', 'reserved', 'reserved_by', ARGV[1])
    return 1            -- reserved by us
else
    return 0            -- already taken
end
"""


def scenario_lua():
    header("3. LUA script  (atomic multi-step check-and-set)  -- FIX")
    r.delete(SEAT_H)
    r.hset(SEAT_H, mapping={"status": "available", "reserved_by": ""})
    winners = []
    barrier = threading.Barrier(2)
    reserve = r.register_script(RESERVE_LUA)

    def worker(name):
        barrier.wait()
        if reserve(keys=[SEAT_H], args=[name]) == 1:
            winners.append(name)

    run_two(worker)
    report(winners)
    print("  WHY: a Lua script is executed atomically and in isolation, so the HGET")
    print("       check and the HSET act cannot be interleaved by another client.")
    print("       Use this when your check-and-set is more than one command.")


# --------------------------------------------------------------------------- #
# 4. WATCH / MULTI / EXEC — OPTIMISTIC
# --------------------------------------------------------------------------- #
def scenario_watch():
    header("4. WATCH / MULTI / EXEC  (optimistic transaction)  -- FIX")
    r.delete(SEAT_H)
    r.hset(SEAT_H, mapping={"status": "available", "reserved_by": ""})
    winners = []
    outcomes = []
    barrier = threading.Barrier(2)

    def worker(name):
        with r.pipeline() as pipe:
            barrier.wait()
            try:
                pipe.watch(SEAT_H)                 # optimistic: watch for changes
                status = pipe.hget(SEAT_H, "status")
                if status == "available":
                    time.sleep(0.05)               # widen window so both race
                    pipe.multi()
                    pipe.hset(SEAT_H, "status", "reserved")
                    pipe.hset(SEAT_H, "reserved_by", name)
                    pipe.execute()                 # EXEC fails if SEAT_H changed
                    winners.append(name)
                    outcomes.append(f"{name}: committed")
                else:
                    outcomes.append(f"{name}: saw 'reserved' -> skipped")
            except redis.exceptions.WatchError:
                # someone modified SEAT_H between WATCH and EXEC -> abort & retry
                outcomes.append(f"{name}: WatchError -> retry")

    run_two(worker)
    print(f"  outcomes: {outcomes}")
    report(winners)
    print("  WHY: WATCH makes EXEC conditional on the key being untouched. If the")
    print("       other client wrote first, EXEC returns nil -> WatchError. This is")
    print("       Redis's built-in optimistic concurrency (compare-and-swap on a key).")


if __name__ == "__main__":
    wait_for_redis()
    scenario_get_then_set()
    scenario_setnx()
    scenario_lua()
    scenario_watch()
    print("\nDone. Redis still on localhost:6380 (redis-cli -p 6380).\n")
