import argparse
import random
from collections import Counter
import statistics


def make_args():
    p = argparse.ArgumentParser(description="In-memory chat fanout simulation")
    p.add_argument("--num_redis_shards", type=int, default=1000)
    p.add_argument("--num_websockets", type=int, default=10000)
    p.add_argument("--seed", type=int, default=12345)
    return p.parse_args()


def build_chat_groups():
    return [
        {
            "name": "giant",
            "num": 1000,
            "min_members": 5000,
            "max_members": 10000,
            "min_msgs": 5,
            "max_msgs": 20,
        },
    ]


def create_chats(groups, seed):
    random.seed(seed + 1)
    global_user_counter = 0

    def new_user():
        nonlocal global_user_counter
        user_id = f"user{global_user_counter}"
        global_user_counter += 1
        return user_id

    chats = []
    all_members = set()

    for g in groups:
        for _ in range(g["num"]):
            size = random.randint(g["min_members"], g["max_members"])
            members = [new_user() for _ in range(size)]
            random.shuffle(members)

            chats.append(
                {
                    "chat_id": len(chats),
                    "members": set(members),
                    "min_msgs": g["min_msgs"],
                    "max_msgs": g["max_msgs"],
                }
            )

            all_members.update(members)

    return chats, all_members


def assign_chats_to_ws_and_shards(chats, num_websockets, num_redis_shards, seed):
    """
    Assign each chat to a single WebSocket server and a single Redis shard.
    All users of a chat inherit the same shard and WS server.
    """
    random.seed(seed + 2)

    chat_to_ws = {}
    chat_to_shard = {}
    shard_store = [dict() for _ in range(num_redis_shards)]
    user_to_ws = {}
    user_to_shard = {}

    for c in chats:
        ws = random.randrange(num_websockets)
        shard = random.randrange(num_redis_shards)
        chat_to_ws[c["chat_id"]] = ws
        chat_to_shard[c["chat_id"]] = shard

        for user in c["members"]:
            user_to_ws[user] = ws
            user_to_shard[user] = shard
            shard_store[shard][user] = ws

    return chat_to_ws, chat_to_shard, user_to_ws, user_to_shard, shard_store


def run_message_cycle(chats, shard_store, user_to_shard, seed):
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

            # Collect shards used by this chat (only one now!)
            shards = set(user_to_shard[u] for u in members)
            for shard in shards:
                redis_reads_per_shard[shard] += 1
                for u in members:
                    ws = shard_store[shard].get(u)
                    if ws is not None:
                        notified_ws.add(ws)

            for ws in notified_ws:
                ws_updates[ws] += 1

    return redis_reads_per_shard, ws_updates, total_chat_messages


VERBOSE = False


def main():
    args = make_args()
    random.seed(args.seed)

    groups = build_chat_groups()
    print("Creating chats and assigning globally unique members...\n")

    chats, users = create_chats(groups, args.seed)
    print(f"Total chats created: {len(chats)}")
    print(f"Total unique users generated: {len(users)}\n")
    print("Assigning chats to websocket servers and redis shards...\n")

    chat_to_ws, chat_to_shard, user_to_ws, user_to_shard, shard_store = (
        assign_chats_to_ws_and_shards(
            chats, args.num_websockets, args.num_redis_shards, args.seed
        )
    )

    print("Running message cycle (resolving users -> shards -> websocket servers)...\n")

    redis_reads_per_shard, ws_updates, total_chat_messages = run_message_cycle(
        chats,
        shard_store,
        user_to_shard,
        args.seed,
    )

    total_redis_reads = sum(redis_reads_per_shard.values())
    total_notifications_sent = sum(ws_updates.values())

    # Calculate users per websocket server
    users_per_ws = {ws_id: 0 for ws_id in range(args.num_websockets)}
    for _, ws_id in user_to_ws.items():
        users_per_ws[ws_id] = users_per_ws.get(ws_id, 0) + 1

    print("=== Simulation Summary ===\n")
    print(f"chat_groups_configured: {len(groups)}")
    print(f"total_chats_created: {len(chats)}")
    print(f"total_unique_users: {len(users)}")
    print(f"num_websockets: {args.num_websockets}")
    print(f"num_redis_shards: {args.num_redis_shards}")
    print(f"total_chat_messages_authored: {total_chat_messages}")
    print(f"total_redis_reads: {total_redis_reads}")
    print(f"total_notifications_sent_to_websockets: {total_notifications_sent}\n")

    def summarize(label, data_dict):
        values = list(data_dict.values())
        if not values:
            print(f"{label}: no data")
            return
        print(f"{label}:")
        print(f"  min: {min(values)}")
        print(f"  max: {max(values)}")
        print(f"  median: {statistics.median(values):.2f}")
        print(f"  average: {statistics.mean(values):.2f}")
        if VERBOSE:
            for k, v in data_dict.items():
                print(f"    {label[:-1]} {k}: {v}")
        print()

    summarize("Redis shard reads", redis_reads_per_shard)
    summarize("Websocket updates", ws_updates)
    summarize("Users per websocket server", users_per_ws)

    print("\nDone.")


if __name__ == "__main__":
    main()
