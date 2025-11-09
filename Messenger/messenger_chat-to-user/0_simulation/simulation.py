#!/usr/bin/env python3
"""
chat_simulation.py

In-memory POC simulation for chat -> redis -> websocket fanout behavior.

Produces:
 - printed summary
 - redis_shard_reads.csv (shard,reads)
 - websocket_updates.csv (websocket,updates)

Configurable via command-line flags (see --help).
"""

import argparse
import random
import csv
from collections import Counter


def make_args():
    p = argparse.ArgumentParser(description="In-memory chat fanout simulation")
    p.add_argument(
        "--num_chats", type=int, default=20000, help="Total number of chats to create"
    )
    p.add_argument(
        "--total_users", type=int, default=120000, help="Global user pool size"
    )
    p.add_argument(
        "--num_websockets", type=int, default=112, help="Number of websocket servers"
    )
    p.add_argument(
        "--num_redis_shards", type=int, default=20, help="Number of redis shards"
    )
    p.add_argument(
        "--seed", type=int, default=12345, help="Random seed for reproducibility"
    )
    p.add_argument(
        "--out_prefix", type=str, default="chat_sim", help="Prefix for output CSV files"
    )
    return p.parse_args()


def build_chat_groups(num_chats):
    """
    Build the desired group breakdown per the user's specification:
      - 5 chats with 20k-50k members (biggest)
      - 25 chats with 1k-5k members
      - 350 chats with 100-1k members
      - 4000 chats with 1-100 members
      - remaining chats (to reach num_chats) assigned 1-100 members
    Each chat entry also carries min/max messages for the message cycle.
    """
    specified_groups = [
        {
            "count": 5,
            "min_members": 20000,
            "max_members": 50000,
            "min_msgs": 5,
            "max_msgs": 20,
        },
        {
            "count": 25,
            "min_members": 1000,
            "max_members": 5000,
            "min_msgs": 2,
            "max_msgs": 10,
        },
        {
            "count": 350,
            "min_members": 100,
            "max_members": 1000,
            "min_msgs": 1,
            "max_msgs": 5,
        },
        {
            "count": 4000,
            "min_members": 1,
            "max_members": 100,
            "min_msgs": 0,
            "max_msgs": 1,
        },
    ]
    sum_specified = sum(g["count"] for g in specified_groups)
    remaining = max(0, num_chats - sum_specified)
    default_small_group = {
        "count": remaining,
        "min_members": 1,
        "max_members": 100,
        "min_msgs": 0,
        "max_msgs": 1,
    }
    return specified_groups + [default_small_group]


def create_chats(all_groups, user_pool, seed):
    """
    Create chat objects: a list of dicts with 'chat_id', 'members' (set of user ids),
    and message min/max.
    """
    random.seed(seed + 1)
    chats = []
    chat_id = 0
    for g in all_groups:
        for _ in range(g["count"]):
            # ensure we don't request a sample bigger than the pool size
            requested = min(g["max_members"], len(user_pool))
            # size chosen uniformly between min_members and max_members, clipped to pool size
            sz = random.randint(g["min_members"], g["max_members"])
            if sz > len(user_pool):
                # if requested size bigger than pool, we'll sample with replacement (less realistic)
                # but better to expand the user_pool before calling create_chats if you want strict uniqueness
                members = set(random.choices(user_pool, k=sz))
            else:
                members = set(random.sample(user_pool, sz))
            chats.append(
                {
                    "chat_id": chat_id,
                    "members": members,
                    "min_msgs": g["min_msgs"],
                    "max_msgs": g["max_msgs"],
                }
            )
            chat_id += 1
    return chats


def assign_users_to_ws_and_shards(total_users, num_websockets, num_redis_shards, seed):
    """
    Assign every user to a websocket server and a redis shard (both chosen randomly).
    Return dictionaries:
      user_to_ws, user_to_shard, shard_store (list of dict per shard: user->ws)
    """
    random.seed(seed + 2)
    user_to_ws = {}
    user_to_shard = {}
    shard_store = [dict() for _ in range(num_redis_shards)]
    for user in range(total_users):
        ws = random.randrange(num_websockets)
        shard = random.randrange(num_redis_shards)
        user_to_ws[user] = ws
        user_to_shard[user] = shard
        shard_store[shard][user] = ws
    return user_to_ws, user_to_shard, shard_store


def run_message_cycle(
    chats, shard_store, user_to_shard, num_redis_shards, num_websockets, seed
):
    """
    For each chat, randomly pick number of messages in the chat's configured range.
    For each message:
      - resolve each user's shard (count read)
      - get websocket for user from shard data (simulate read)
      - dedupe websocket servers and send one notification per server
    Returns counters: redis_reads_per_shard, ws_updates, total_messages_authored
    """
    random.seed(seed + 3)
    redis_reads_per_shard = Counter()
    ws_updates = Counter()
    total_chat_messages = 0

    for c in chats:
        msgs = random.randint(c["min_msgs"], c["max_msgs"])
        if msgs == 0:
            continue
        members = c["members"]
        for _ in range(msgs):
            total_chat_messages += 1
            notified_ws = set()
            # naive per-user lookups: count a read for each user's shard
            for user in members:
                shard = user_to_shard.get(user)
                # if shard missing (should not happen) skip
                if shard is None:
                    continue
                redis_reads_per_shard[shard] += 1
                # fetch ws mapping from the shard store
                ws = shard_store[shard].get(user)
                if ws is None:
                    continue
                notified_ws.add(ws)
            # one notification per websocket server
            for ws in notified_ws:
                ws_updates[ws] += 1

    return redis_reads_per_shard, ws_updates, total_chat_messages


def dump_csv_rows(filename, rows, headers):
    with open(filename, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(headers)
        for r in rows:
            w.writerow(r)


def main():
    args = make_args()
    random.seed(args.seed)

    # Build chat groups
    all_groups = build_chat_groups(args.num_chats)

    # Ensure user pool is large enough to satisfy the maximum chat size in groups:
    max_chat_size = max(g["max_members"] for g in all_groups)
    if args.total_users < max_chat_size:
        print(
            f"Warning: total_users ({args.total_users}) < largest chat max size ({max_chat_size})."
        )
        print(
            "Expanding user pool to match largest chat to avoid sampling-with-replacement."
        )
        total_users = max_chat_size
    else:
        total_users = args.total_users

    # Create global user pool
    user_pool = list(range(total_users))

    # Create chats (list of dicts)
    print("Creating chats and assigning members...")
    chats = create_chats(all_groups, user_pool, args.seed)

    num_chats_actual = len(chats)
    unique_users_in_chats = set()
    for c in chats:
        unique_users_in_chats.update(c["members"])
    num_unique_users = len(unique_users_in_chats)

    # Assign users to websocket servers and redis shards
    print("Assigning users to websocket servers and redis shards...")
    user_to_ws, user_to_shard, shard_store = assign_users_to_ws_and_shards(
        total_users, args.num_websockets, args.num_redis_shards, args.seed
    )

    # Run one cycle of messages being sent and delivered
    print("Running message cycle (resolving users -> shards -> websocket servers)...")
    redis_reads_per_shard, ws_updates, total_chat_messages = run_message_cycle(
        chats,
        shard_store,
        user_to_shard,
        args.num_redis_shards,
        args.num_websockets,
        args.seed,
    )

    total_redis_reads = sum(redis_reads_per_shard.values())
    total_notifications_sent = sum(ws_updates.values())

    # Print summary
    print("\n=== Simulation Summary ===")
    print(f"configured_num_chats: {args.num_chats}")
    print(f"actual_chats_created: {num_chats_actual}")
    print(f"configured_total_users: {args.total_users}")
    print(f"actual_total_users_pool: {total_users}")
    print(f"unique_users_in_chats: {num_unique_users}")
    print(f"num_websockets: {args.num_websockets}")
    print(f"num_redis_shards: {args.num_redis_shards}")
    print(f"total_chat_messages_authored: {total_chat_messages}")
    print(f"total_redis_reads: {total_redis_reads}")
    print(f"total_notifications_sent_to_websockets: {total_notifications_sent}")

    # Prepare CSV outputs
    shard_rows = [
        (shard, redis_reads_per_shard.get(shard, 0))
        for shard in range(args.num_redis_shards)
    ]
    ws_rows = [(ws, ws_updates.get(ws, 0)) for ws in range(args.num_websockets)]

    shard_csv = f"{args.out_prefix}_redis_shard_reads.csv"
    ws_csv = f"{args.out_prefix}_websocket_updates.csv"

    dump_csv_rows(shard_csv, shard_rows, headers=["shard", "reads"])
    dump_csv_rows(ws_csv, ws_rows, headers=["websocket", "updates"])

    print(f"\nCSV files written:\n - {shard_csv}\n - {ws_csv}")
    print("Done.")


if __name__ == "__main__":
    main()
