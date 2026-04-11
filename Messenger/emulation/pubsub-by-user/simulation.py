#!/usr/bin/env python3
"""
Messenger Simulation — Pub/Sub by User
=======================================
Flow per message:
  1. Message arrives at ChatAPI (one per chat).
  2. ChatAPI -> chat-user storage  : get users in the chat.
  3. ChatAPI enqueues message into each participant's personal user queue.
  4. Each user queue notifies its single subscribed GW node.
  5. GW delivers to the user's socket (in-memory lookup).

Key difference from direct-gw-append:
  - ChatAPI does NOT query user-gw storage.
  - Routing is implicit: each GW subscribed to user queues at connect time.
  - The message broker (queue layer) handles routing, not ChatAPI.
"""

import hashlib
from typing import Dict, List, Optional

# ----------------------------------------------------------------
# CONFIGURATION
# ----------------------------------------------------------------

NUM_CHATS            = 10  # also the number of ChatAPI instances
USERS_PER_CHAT       = 10000

NUM_GATEWAY_NODES    = 1000
NUM_CHAT_USER_SHARDS = 1000
MESSAGES_PER_CHAT    = 10

DETAILED_OUTPUT      = False   # False → summary percentile stats only

# ----------------------------------------------------------------
# LOGGING
# ----------------------------------------------------------------

_INDENT = "    "


def log(component: str, msg: str, level: int = 0) -> None:
    if DETAILED_OUTPUT:
        print(f"{_INDENT * level}[{component}] {msg}")


def header(title: str) -> None:
    if DETAILED_OUTPUT:
        print(f"\n{'-' * 62}")
        print(f"  {title}")
        print(f"{'-' * 62}")


def _percentile(data: List[float], p: float) -> float:
    if not data:
        return 0.0
    s = sorted(data)
    idx = p / 100 * (len(s) - 1)
    lo, hi = int(idx), min(int(idx) + 1, len(s) - 1)
    return s[lo] + (idx - lo) * (s[hi] - s[lo])


# ----------------------------------------------------------------
# SOCKET — simulated WebSocket connection
# ----------------------------------------------------------------

class Socket:
    """Simulated WebSocket connection between a User and a GW node."""

    def __init__(self, socket_id: str, user_id: str) -> None:
        self.socket_id = socket_id
        self.user_id   = user_id

        self.messages_received:     List[dict] = []
        self.successful_deliveries: int = 0

    def deliver(self, message: str, chat_id: str) -> None:
        self.messages_received.append({"chat_id": chat_id, "message": message})
        self.successful_deliveries += 1
        log(
            f"SOCKET {self.socket_id}",
            f"OK Delivered to {self.user_id}: '{message}' in {chat_id}",
            level=4,
        )

    def __repr__(self) -> str:
        return f"Socket({self.socket_id}, user={self.user_id})"


# ----------------------------------------------------------------
# USER
# ----------------------------------------------------------------

class User:
    def __init__(self, user_id: str) -> None:
        self.user_id = user_id
        self.socket:  Optional[Socket] = None
        self.gw_id:   Optional[str]    = None

    def attach(self, socket: Socket, gw_id: str) -> None:
        self.socket = socket
        self.gw_id  = gw_id

    def __repr__(self) -> str:
        return f"User({self.user_id}, gw={self.gw_id})"


# ----------------------------------------------------------------
# USER QUEUE — personal queue per user, one GW subscribes
# ----------------------------------------------------------------

class UserQueue:
    """
    Personal message queue for exactly one user.
    Exactly one GW subscribes at user connect time.
    When a message is enqueued, the subscriber GW is notified immediately.
    """

    def __init__(self, user_id: str) -> None:
        self.user_id       = user_id
        self.subscriber:   Optional["GatewayNode"] = None
        self.enqueue_count = 0
        self.notify_count  = 0

    def subscribe(self, gw: "GatewayNode") -> None:
        self.subscriber = gw
        log("UserQueue", f"GW {gw.gw_id} subscribed to queue for {self.user_id}", level=1)

    def enqueue(self, message: str, chat_id: str) -> None:
        """Called by ChatAPI. Notifies the subscribed GW immediately."""
        self.enqueue_count += 1
        log("UserQueue", f"enqueue({self.user_id}): '{message}' (chat={chat_id})", level=2)
        if self.subscriber is not None:
            self.notify_count += 1
            self.subscriber.on_user_message(self.user_id, message, chat_id)

    def __repr__(self) -> str:
        return f"UserQueue({self.user_id})"


# ----------------------------------------------------------------
# CHAT-USER STORAGE — sharded by chatId
# ----------------------------------------------------------------

class ChatUserStorage:
    """Persistent store: chatId -> [userId].  Sharded by chatId."""

    def __init__(self, num_shards: int) -> None:
        self.num_shards  = num_shards
        self._shards: List[Dict[str, List[str]]] = [{} for _ in range(num_shards)]
        self.query_count = 0
        self.write_count = 0
        self._shard_query_counts: List[int] = [0] * num_shards

    def _shard(self, chat_id: str) -> int:
        return int(hashlib.md5(chat_id.encode()).hexdigest(), 16) % self.num_shards

    def add_user(self, chat_id: str, user_id: str) -> None:
        s = self._shard(chat_id)
        self.write_count += 1
        bucket = self._shards[s].setdefault(chat_id, [])
        if user_id not in bucket:
            bucket.append(user_id)

    def get_users(self, chat_id: str) -> List[str]:
        s = self._shard(chat_id)
        self.query_count += 1
        self._shard_query_counts[s] += 1
        result = self._shards[s].get(chat_id, [])
        log("ChatUserStorage", f"get_users({chat_id}) -> shard {s} -> {result}", level=2)
        return result


# ----------------------------------------------------------------
# GATEWAY NODE
# ----------------------------------------------------------------

class GatewayNode:
    """
    At connect time:
      - Registers the user's socket (in-memory).
      - Subscribes to the user's personal queue.

    On queue notification (on_user_message):
      - Looks up the socket for that user (in-memory).
      - Delivers the message directly.

    No storage queries at message-delivery time.
    """

    def __init__(self, gw_id: str) -> None:
        self.gw_id = gw_id
        self.user_socket: Dict[str, Socket] = {}

        self.total_connections      = 0
        self.notifications_received = 0
        self.messages_delivered     = 0

    @property
    def active_connections(self) -> int:
        return len(self.user_socket)

    def connect_user(self, user: User, socket: Socket, queue: UserQueue) -> None:
        """Register user socket and subscribe to their personal queue."""
        self.user_socket[user.user_id] = socket
        self.total_connections += 1
        queue.subscribe(self)
        log(
            f"GW {self.gw_id}",
            f"User {user.user_id} connected via {socket.socket_id}, subscribed to queue",
            level=1,
        )

    def on_user_message(self, user_id: str, message: str, chat_id: str) -> None:
        """Called by UserQueue when a new message is enqueued for this user."""
        self.notifications_received += 1
        log(
            f"GW {self.gw_id}",
            f"Queue notify: user={user_id}, msg='{message}', chat={chat_id}",
            level=2,
        )

        socket = self.user_socket.get(user_id)
        if socket is None:
            log(f"GW {self.gw_id}", f"WARNING: no socket for {user_id}", level=2)
            return

        socket.deliver(message, chat_id)
        self.messages_delivered += 1


# ----------------------------------------------------------------
# CHAT API — one instance per chat
# ----------------------------------------------------------------

class ChatAPI:
    """
    On each incoming message:
      1. Queries chat-user storage -> list of participants.
      2. Enqueues the message into each participant's personal user queue.
         (No user-gw lookup — routing is handled by queue subscriptions.)
    """

    def __init__(
        self,
        api_id:            str,
        chat_id:           str,
        chat_user_storage: ChatUserStorage,
        user_queues:       Dict[str, UserQueue],
    ) -> None:
        self.api_id            = api_id
        self.chat_id           = chat_id
        self.chat_user_storage = chat_user_storage
        self.user_queues       = user_queues

        self.messages_received = 0
        self.enqueue_calls     = 0

    def send_message(self, sender_id: str, message: str) -> None:
        log(
            f"ChatAPI {self.api_id}",
            f"> message from {sender_id}: '{message}'  (chat={self.chat_id})",
        )
        self.messages_received += 1

        # ① Get users in this chat
        users = self.chat_user_storage.get_users(self.chat_id)
        log(f"ChatAPI {self.api_id}", f"users in {self.chat_id}: {users}", level=1)

        # ② Enqueue into each participant's personal queue
        for user_id in users:
            queue = self.user_queues.get(user_id)
            if queue is not None:
                self.enqueue_calls += 1
                queue.enqueue(message, self.chat_id)


# ----------------------------------------------------------------
# SIMULATION
# ----------------------------------------------------------------

class Simulation:
    def __init__(
        self,
        num_chats:         int = NUM_CHATS,
        users_per_chat:    int = USERS_PER_CHAT,
        num_gw_nodes:      int = NUM_GATEWAY_NODES,
        num_cu_shards:     int = NUM_CHAT_USER_SHARDS,
        messages_per_chat: int = MESSAGES_PER_CHAT,
    ) -> None:
        self.num_chats         = num_chats
        self.users_per_chat    = users_per_chat
        self.num_gw_nodes      = num_gw_nodes
        self.messages_per_chat = messages_per_chat

        self.chat_user_storage = ChatUserStorage(num_cu_shards)

        self.gw_nodes: Dict[str, GatewayNode] = {
            f"GW{i + 1}": GatewayNode(f"GW{i + 1}")
            for i in range(num_gw_nodes)
        }

        self.users:       Dict[str, User]      = {}
        self.user_queues: Dict[str, UserQueue] = {}
        self.chat_apis:   Dict[str, ChatAPI]   = {}
        self.chats:       Dict[str, List[str]] = {}

        self._socket_ctr = 0

    # -- helpers --------------------------------------------------

    def _next_socket(self, user_id: str) -> Socket:
        self._socket_ctr += 1
        return Socket(f"sock{self._socket_ctr}", user_id)

    # -- setup ----------------------------------------------------

    def _setup(self) -> None:
        header("SETUP PHASE")
        gw_ids       = list(self.gw_nodes.keys())
        user_counter = 1

        for chat_idx in range(self.num_chats):
            chat_id  = f"chat{chat_idx + 1}"
            user_ids: List[str] = []

            for _ in range(self.users_per_chat):
                user_id  = f"u{user_counter}"
                gw_id    = gw_ids[(user_counter - 1) % self.num_gw_nodes]
                user_counter += 1

                socket = self._next_socket(user_id)
                user   = User(user_id)
                user.attach(socket, gw_id)

                # Personal queue for this user
                queue = UserQueue(user_id)

                # Persistent: record chat membership
                self.chat_user_storage.add_user(chat_id, user_id)

                # GW subscribes to user queue at connect time (in-memory only)
                self.gw_nodes[gw_id].connect_user(user, socket, queue)

                self.users[user_id]       = user
                self.user_queues[user_id] = queue
                user_ids.append(user_id)

            self.chats[chat_id] = user_ids
            self.chat_apis[chat_id] = ChatAPI(
                api_id            = f"API-{chat_id}",
                chat_id           = chat_id,
                chat_user_storage = self.chat_user_storage,
                user_queues       = self.user_queues,
            )

        if DETAILED_OUTPUT:
            print("\nChats:")
            for cid, uids in self.chats.items():
                print(f"  {cid}: {uids}")
            print("\nGateway subscriptions:")
            for gw_id, gw in self.gw_nodes.items():
                print(f"  {gw_id}: subscribed to users={list(gw.user_socket.keys())}")

    # -- run ------------------------------------------------------

    def run_simulation(self) -> None:
        self._setup()

        header("SIMULATION — SENDING MESSAGES")

        messages = []
        for chat_id, user_ids in self.chats.items():
            for m_idx in range(self.messages_per_chat):
                sender = user_ids[m_idx % len(user_ids)]
                messages.append((chat_id, f"Message {m_idx + 1}", sender))

        for i, (chat_id, msg, sender) in enumerate(messages, 1):
            if DETAILED_OUTPUT:
                print(f"\n  --- Message {i}/{len(messages)} ---")
            self.chat_apis[chat_id].send_message(sender, msg)

        self._print_stats()

    # -- stats ----------------------------------------------------

    def _print_stats(self) -> None:
        if DETAILED_OUTPUT:
            self._print_stats_detailed()
        else:
            self._print_stats_summary()

    def _print_stats_detailed(self) -> None:
        header("SIMULATION STATISTICS")

        print("\n[Chat API — messages & enqueue calls]")
        total_api_msgs = 0
        for chat_id, api in self.chat_apis.items():
            total_api_msgs += api.messages_received
            print(
                f"  {api.api_id}: {api.messages_received} messages, "
                f"{api.enqueue_calls} enqueue calls"
            )
        print(f"  TOTAL messages: {total_api_msgs}")

        print("\n[Gateway Nodes — notifications & deliveries]")
        for gw_id, gw in self.gw_nodes.items():
            print(
                f"  {gw_id}: notifications={gw.notifications_received}, "
                f"delivered={gw.messages_delivered}"
            )

        print("\n[ChatUser Storage — queries per shard]")
        cu = self.chat_user_storage
        for i, count in enumerate(cu._shard_query_counts):
            print(f"  shard {i}: {count} queries")
        print(f"  TOTAL: {cu.query_count} queries")

        print("\n[Per-chat summary]")
        for chat_id, user_ids in self.chats.items():
            api = self.chat_apis[chat_id]
            total_delivered = sum(
                self.users[uid].socket.successful_deliveries
                for uid in user_ids
                if self.users[uid].socket
            )
            print(
                f"  {chat_id}: {api.messages_received} messages sent, "
                f"{total_delivered} deliveries"
            )

    def _print_stats_summary(self) -> None:
        w = 52
        print(f"\n{'=' * w}")
        print("  SIMULATION SUMMARY  [Pub/Sub by User]")
        print(f"{'=' * w}")

        total_users = self.num_chats * self.users_per_chat
        total_msgs  = self.num_chats * self.messages_per_chat

        print("\n[Configuration]")
        print(f"  chats                    : {self.num_chats}")
        print(f"  participants/chat        : {self.users_per_chat:,}")
        print(f"  messages/chat            : {self.messages_per_chat}")
        print(f"  total users              : {total_users:,}")
        print(f"  total user queues        : {total_users:,}")
        print(f"  total messages           : {total_msgs:,}")
        print(f"  GW nodes (total)         : {self.num_gw_nodes}")
        print(f"  ChatUser storage shards  : {self.chat_user_storage.num_shards}")
        print(f"  UserGW storage shards    : 0  (not needed)")

        # GW node load
        gw_notifs    = [gw.notifications_received for gw in self.gw_nodes.values()]
        gw_delivered = [gw.messages_delivered      for gw in self.gw_nodes.values()]
        gw_conns     = [gw.total_connections        for gw in self.gw_nodes.values()]

        print("\n[GW nodes — connections]")
        print(f"  p50={_percentile(gw_conns, 50):.1f}  p95={_percentile(gw_conns, 95):.1f}  p99={_percentile(gw_conns, 99):.1f}  max={max(gw_conns)}")
        print("\n[GW nodes — queue notifications received]")
        print(f"  p50={_percentile(gw_notifs, 50):.1f}  p95={_percentile(gw_notifs, 95):.1f}  p99={_percentile(gw_notifs, 99):.1f}  max={max(gw_notifs)}")
        print("\n[GW nodes — messages delivered]")
        print(f"  p50={_percentile(gw_delivered, 50):.1f}  p95={_percentile(gw_delivered, 95):.1f}  p99={_percentile(gw_delivered, 99):.1f}  max={max(gw_delivered)}")

        # ChatUser storage query distribution
        cu_shards = self.chat_user_storage._shard_query_counts
        print("\n[ChatUser storage — queries per shard]")
        print(
            f"  total={self.chat_user_storage.query_count:,}  "
            f"p50={_percentile(cu_shards, 50):.1f}  "
            f"p99={_percentile(cu_shards, 99):.1f}  "
            f"max={max(cu_shards)}"
        )

        # User queue stats
        enqueue_counts = [q.enqueue_count for q in self.user_queues.values()]
        print("\n[User queues — enqueue count per queue]")
        print(
            f"  total={sum(enqueue_counts):,}  "
            f"p50={_percentile(enqueue_counts, 50):.1f}  "
            f"p99={_percentile(enqueue_counts, 99):.1f}  "
            f"max={max(enqueue_counts)}"
        )

        # Users per GW node
        print("\n[Users per GW node]")
        print(f"  per chat              : {self.users_per_chat / self.num_gw_nodes:.1f}")
        print(f"  total                 : {total_users / self.num_gw_nodes:.1f}")

        # Per-user delivery stats
        user_deliveries = [
            user.socket.successful_deliveries
            for user in self.users.values()
            if user.socket
        ]
        print("\n[Per-user deliveries]")
        print(
            f"  p50={_percentile(user_deliveries, 50):.1f}  "
            f"p95={_percentile(user_deliveries, 95):.1f}  "
            f"p99={_percentile(user_deliveries, 99):.1f}  "
            f"max={max(user_deliveries)}"
        )


# ----------------------------------------------------------------
# ENTRY POINT
# ----------------------------------------------------------------

if __name__ == "__main__":
    sim = Simulation(
        num_chats         = NUM_CHATS,
        users_per_chat    = USERS_PER_CHAT,
        num_gw_nodes      = NUM_GATEWAY_NODES,
        messages_per_chat = MESSAGES_PER_CHAT,
    )
    sim.run_simulation()