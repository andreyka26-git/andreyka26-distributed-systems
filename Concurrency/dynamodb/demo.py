"""
DynamoDB concurrency demo: two threads fight to reserve seat #1.

Scenarios:
  1. LOST UPDATE  -> the BUG. get_item, then put_item unconditionally.
  2. OPTIMISTIC   -> FIX. update_item with ConditionExpression on status.
  3. OPTIMISTIC v -> FIX. update_item with a version attribute.

DynamoDB has NO pessimistic locks (no SELECT ... FOR UPDATE, no blocking). The
ONLY native concurrency control is the OPTIMISTIC conditional write. The loser
gets a ConditionalCheckFailedException and must retry. (TransactWriteItems also
uses conditions and is still optimistic — see README.)
"""
import os
import sys
import time
import threading
import boto3

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass
from botocore.config import Config
from botocore.exceptions import ClientError, EndpointConnectionError

TABLE = "Seats"


def ddb():
    return boto3.resource(
        "dynamodb",
        endpoint_url=os.getenv("DDB_ENDPOINT", "http://localhost:8000"),
        region_name=os.getenv("AWS_REGION", "us-east-1"),
        config=Config(retries={"max_attempts": 0}),  # don't let boto3 auto-retry
    )


def wait_and_create_table(retries=30):
    for _ in range(retries):
        try:
            client = ddb().meta.client
            existing = client.list_tables()["TableNames"]
            if TABLE not in existing:
                client.create_table(
                    TableName=TABLE,
                    KeySchema=[{"AttributeName": "id", "KeyType": "HASH"}],
                    AttributeDefinitions=[{"AttributeName": "id", "AttributeType": "N"}],
                    BillingMode="PAY_PER_REQUEST",
                )
                client.get_waiter("table_exists").wait(TableName=TABLE)
            return ddb().Table(TABLE)
        except (EndpointConnectionError, ClientError):
            time.sleep(1)
    raise SystemExit("DynamoDB Local never became reachable")


table = None


def reset_seat():
    table.put_item(
        Item={"id": 1, "status": "available", "reserved_by": "none", "version": 0}
    )


def final_seat():
    d = table.get_item(Key={"id": 1})["Item"]
    return d["status"], d["reserved_by"], int(d["version"])


def header(title):
    print("\n" + "=" * 78)
    print(title)
    print("=" * 78)


def report(winners, row):
    print(f"  -> winners: {winners}")
    print(f"  -> final item: status={row[0]!r} reserved_by={row[1]!r} version={row[2]}")
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
# 1. LOST UPDATE — THE BUG
# --------------------------------------------------------------------------- #
def scenario_lost_update():
    header("1. LOST UPDATE  (get_item, then unconditional put_item)  -- THE BUG")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        item = table.get_item(Key={"id": 1})["Item"]   # read
        barrier.wait()
        if item["status"] == "available":
            # BUG: a plain put_item has no condition -> it always wins the write,
            # clobbering whatever is there. Check-then-act is not atomic.
            table.put_item(
                Item={"id": 1, "status": "reserved", "reserved_by": name, "version": 0}
            )
            winners.append(name)

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: put_item with no ConditionExpression unconditionally overwrites.")


# --------------------------------------------------------------------------- #
# 2. OPTIMISTIC — ConditionExpression on status
# --------------------------------------------------------------------------- #
def scenario_optimistic_status():
    header("2. OPTIMISTIC  (update_item ConditionExpression status='available')  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        barrier.wait()
        try:
            # The condition is evaluated atomically by DynamoDB at write time.
            # Only the first writer sees status='available'. The second fails.
            table.update_item(
                Key={"id": 1},
                UpdateExpression="SET #s = :reserved, reserved_by = :who",
                ConditionExpression="#s = :available",
                ExpressionAttributeNames={"#s": "status"},
                ExpressionAttributeValues={
                    ":reserved": "reserved",
                    ":who": name,
                    ":available": "available",
                },
            )
            winners.append(name)
        except ClientError as e:
            if e.response["Error"]["Code"] == "ConditionalCheckFailedException":
                pass  # we lost the race -> in real code, return "seat taken"/retry
            else:
                raise

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: ConditionExpression makes the write a single atomic compare-and-set.")
    print("       Loser -> ConditionalCheckFailedException.")


# --------------------------------------------------------------------------- #
# 3. OPTIMISTIC — version attribute
# --------------------------------------------------------------------------- #
def scenario_optimistic_version():
    header("3. OPTIMISTIC  (version attribute)  -- FIX")
    reset_seat()
    winners = []
    barrier = threading.Barrier(2)

    def worker(name):
        item = table.get_item(Key={"id": 1})["Item"]
        version = int(item["version"])
        barrier.wait()
        try:
            table.update_item(
                Key={"id": 1},
                UpdateExpression="SET #s=:r, reserved_by=:w, version=:nv",
                ConditionExpression="version = :ov",   # only if version unchanged
                ExpressionAttributeNames={"#s": "status"},
                ExpressionAttributeValues={
                    ":r": "reserved", ":w": name,
                    ":nv": version + 1, ":ov": version,
                },
            )
            winners.append(name)
        except ClientError as e:
            if e.response["Error"]["Code"] == "ConditionalCheckFailedException":
                pass
            else:
                raise

    run_two(worker)
    report(winners, final_seat())
    print("  WHY: classic optimistic locking — bump version only if it matches the")
    print("       one you read. The DynamoDBMapper @DynamoDBVersionAttribute does this.")


if __name__ == "__main__":
    table = wait_and_create_table()
    scenario_lost_update()
    scenario_optimistic_status()
    scenario_optimistic_version()
    print("\nDone. DynamoDB Local still on localhost:8000.\n")
