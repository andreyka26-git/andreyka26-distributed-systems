#!/usr/bin/env python3
"""
Messenger Simulation — Pub/Sub by Chat
=======================================
Flow per message:
  1. Message arrives at ChatAPI (one per chat).
  2. ChatAPI enqueues message into the chat's queue (one per chat).
  3. Chat queue broadcasts to ALL subscribed GW nodes (all 4 get the message).
  4. Each GW fans out to its locally-connected users who are in that chat.

Key differences from pub/sub by user:
  - One queue per CHAT (not per user).  3 queues total vs N_users queues.
  - ALL GW nodes subscribe to every chat queue.
  - No chat-user storage query at send time — ChatAPI just enqueues.
  - No user-gw storage at all — GW tracks chat membership in-memory.
  - GW does a local fan-out only to its own connected users in that chat.
"""

import hashlib
from typing import Dict, List, Optional, Set

# ----------------------------------------------------------------
# CONFIGURATION
# ----------------------------------------------------------------

NUM_CHATS            = 10
USERS_PER_CHAT       = 10000

NUM_GATEWAY_NODES    = 1000
MESSAGES_PER_CHAT    = 10

DETAILED_OUTPUT      = False   # True → per-step detail; False → summary only

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
# CHAT QUEUE — one per chat, all GW nodes subscribe
# ----------------------------------------------------------------

class ChatQueue:
    """
    Message queue for one chat.
    All GW nodes subscribe at user connect time.
    On enqueue, the message is broadcast to ALL subscribed GW nodes;
    each GW then fans out locally to its connected users in this chat.
    """

    def __init__(self, chat_id: str) -> None:
        self.chat_id    = chat_id
        self.subscribers: List["GatewayNode"] = []
        self.enqueue_count   = 0
        self.broadcast_count = 0   # total GW notifications sent

    def subscribe(self, gw: "GatewayNode") -> None:
        if gw not in self.subscribers:
            self.subscribers.append(gw)
            log("ChatQueue", f"GW {gw.gw_id} subscribed to queue for {self.chat_id}", level=1)

    def enqueue(self, message: str, sender_id: str) -> None:
        """Called by ChatAPI. Broadcasts to all subscribed GW nodes."""
        self.enqueue_count += 1
        log("ChatQueue", f"enqueue({self.chat_id}): '{message}' from {sender_id}", level=2)
        for gw in self.subscribers:
            self.broadcast_count += 1
            gw.on_chat_message(self.chat_id, message, sender_id)

    def __repr__(self) -> str:
        return f"ChatQueue({self.chat_id}, subs={len(self.subscribers)})"


# ----------------------------------------------------------------
# GATEWAY NODE
# ----------------------------------------------------------------

class GatewayNode:
    """
    At user connect time:
      - Registers the user's socket (in-memory).
      - Records which chat this user is in (in-memory).
      - Subscribes to that chat's queue (if not already subscribed).

    On queue broadcast (on_chat_message):
      - Looks up all locally-connected users in that chat (in-memory).
      - Delivers to each one via their socket.

    No storage queries at any point in the delivery path.
    """

    def __init__(self, gw_id: str) -> None:
        self.gw_id = gw_id
        self.user_socket: Dict[str, Socket] = {}          # user_id -> Socket
        self.chat_users:  Dict[str, List[str]] = {}        # chat_id -> [user_id]
        self._subscribed_queues: Set[str] = set()          # set of chat_ids

        self.total_connections      = 0
        self.notifications_received = 0
        self.messages_delivered     = 0

    @property
    def active_connections(self) -> int:
        return len(self.user_socket)

    def connect_user(
        self,
        user:       User,
        socket:     Socket,
        chat_id:    str,
        chat_queue: ChatQueue,
    ) -> None:
        """Register user socket, track chat membership, subscribe to chat queue."""
        self.user_socket[user.user_id] = socket
        self.chat_users.setdefault(chat_id, []).append(user.user_id)
        self.total_connections += 1

        if chat_id not in self._subscribed_queues:
            self._subscribed_queues.add(chat_id)
            chat_queue.subscribe(self)

        log(
            f"GW {self.gw_id}",
            f"User {user.user_id} connected ({socket.socket_id}), "
            f"subscribed to queue for {chat_id}",
            level=1,
        )

    def on_chat_message(self, chat_id: str, message: str, sender_id: str) -> None:
        """Called by ChatQueue broadcast. Fan-out to locally-connected users in this chat."""
        self.notifications_received += 1
        users_in_chat = self.chat_users.get(chat_id, [])
        log(
            f"GW {self.gw_id}",
            f"Broadcast received: chat={chat_id}, msg='{message}', "
            f"local users={users_in_chat}",
            level=2,
        )
        for user_id in users_in_chat:
            socket = self.user_socket.get(user_id)
            if socket is not None:
                socket.deliver(message, chat_id)
                self.messages_delivered += 1


# ----------------------------------------------------------------
# CHAT API — one instance per chat
# ----------------------------------------------------------------

class ChatAPI:
    """
    On each incoming message:
      1. Enqueues message into this chat's queue.
         (No storage lookup — routing is handled by queue subscriptions.)
    """

    def __init__(self, api_id: str, chat_id: str, chat_queue: ChatQueue) -> None:
        self.api_id     = api_id
        self.chat_id    = chat_id
        self.chat_queue = chat_queue

        self.messages_received = 0

    def send_message(self, sender_id: str, message: str) -> None:
        log(
            f"ChatAPI {self.api_id}",
            f"> message from {sender_id}: '{message}'  (chat={self.chat_id})",
        )
        self.messages_received += 1
        self.chat_queue.enqueue(message, sender_id)


# ----------------------------------------------------------------
# SIMULATION
# ----------------------------------------------------------------

class Simulation:
    def __init__(
        self,
        num_chats:         int = NUM_CHATS,
        users_per_chat:    int = USERS_PER_CHAT,
        num_gw_nodes:      int = NUM_GATEWAY_NODES,
        messages_per_chat: int = MESSAGES_PER_CHAT,
    ) -> None:
        self.num_chats         = num_chats
        self.users_per_chat    = users_per_chat
        self.num_gw_nodes      = num_gw_nodes
        self.messages_per_chat = messages_per_chat

        self.gw_nodes: Dict[str, GatewayNode] = {
            f"GW{i + 1}": GatewayNode(f"GW{i + 1}")
            for i in range(num_gw_nodes)
        }

        self.users:       Dict[str, User]      = {}
        self.chat_queues: Dict[str, ChatQueue] = {}
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
            chat_id = f"chat{chat_idx + 1}"

            # One queue and one API per chat
            queue = ChatQueue(chat_id)
            self.chat_queues[chat_id] = queue
            self.chat_apis[chat_id]   = ChatAPI(
                api_id     = f"API-{chat_id}",
                chat_id    = chat_id,
                chat_queue = queue,
            )

            user_ids: List[str] = []

            for _ in range(self.users_per_chat):
                user_id = f"u{user_counter}"
                gw_id   = gw_ids[(user_counter - 1) % self.num_gw_nodes]
                user_counter += 1

                socket = self._next_socket(user_id)
                user   = User(user_id)
                user.attach(socket, gw_id)

                # GW registers user and subscribes to chat queue
                self.gw_nodes[gw_id].connect_user(user, socket, chat_id, queue)

                self.users[user_id] = user
                user_ids.append(user_id)

            self.chats[chat_id] = user_ids

        if DETAILED_OUTPUT:
            print("\nChats:")
            for cid, uids in self.chats.items():
                print(f"  {cid}: {uids}")
            print("\nGW subscriptions:")
            for gw_id, gw in self.gw_nodes.items():
                print(f"  {gw_id}: subscribed chats={list(gw._subscribed_queues)}, "
                      f"users={list(gw.user_socket.keys())}")
            print("\nChat queues (subscribers):")
            for cid, q in self.chat_queues.items():
                print(f"  {cid}: [{', '.join(g.gw_id for g in q.subscribers)}]")

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

        print("\n[Chat API — messages enqueued]")
        for chat_id, api in self.chat_apis.items():
            q = self.chat_queues[chat_id]
            print(
                f"  {api.api_id}: {api.messages_received} messages, "
                f"queue broadcasts={q.broadcast_count} "
                f"({q.broadcast_count // max(api.messages_received, 1)} GWs × {api.messages_received} msgs)"
            )

        print("\n[Chat Queues — enqueue & broadcast counts]")
        for cid, q in self.chat_queues.items():
            print(
                f"  {cid}: enqueue_count={q.enqueue_count}, "
                f"broadcast_count={q.broadcast_count}, "
                f"subscribers={[g.gw_id for g in q.subscribers]}"
            )

        print("\n[Gateway Nodes — notifications & deliveries]")
        for gw_id, gw in self.gw_nodes.items():
            print(
                f"  {gw_id}: notifications={gw.notifications_received}, "
                f"delivered={gw.messages_delivered}, "
                f"local_users={list(gw.user_socket.keys())}"
            )

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
                f"{total_delivered} deliveries "
                f"({total_delivered // max(api.messages_received, 1)} recipients/msg)"
            )

    def _print_stats_summary(self) -> None:
        w = 54
        print(f"\n{'=' * w}")
        print("  SIMULATION SUMMARY  [Pub/Sub by Chat]")
        print(f"{'=' * w}")

        total_users = self.num_chats * self.users_per_chat
        total_msgs  = self.num_chats * self.messages_per_chat

        print("\n[Configuration]")
        print(f"  chats                    : {self.num_chats}")
        print(f"  participants/chat        : {self.users_per_chat:,}")
        print(f"  messages/chat            : {self.messages_per_chat}")
        print(f"  total users              : {total_users:,}")
        print(f"  total chat queues        : {self.num_chats}  (one per chat)")
        print(f"  total messages           : {total_msgs:,}")
        print(f"  GW nodes (total)         : {self.num_gw_nodes}")
        print(f"  ChatUser storage shards  : 0  (not needed)")
        print(f"  UserGW storage shards    : 0  (not needed)")

        # GW node load
        gw_notifs    = [gw.notifications_received for gw in self.gw_nodes.values()]
        gw_delivered = [gw.messages_delivered      for gw in self.gw_nodes.values()]
        gw_conns     = [gw.total_connections        for gw in self.gw_nodes.values()]

        print("\n[GW nodes — connections]")
        print(f"  p50={_percentile(gw_conns, 50):.1f}  p95={_percentile(gw_conns, 95):.1f}  p99={_percentile(gw_conns, 99):.1f}  max={max(gw_conns)}")
        print("\n[GW nodes — chat queue notifications received]")
        print(f"  p50={_percentile(gw_notifs, 50):.1f}  p95={_percentile(gw_notifs, 95):.1f}  p99={_percentile(gw_notifs, 99):.1f}  max={max(gw_notifs)}")
        print("\n[GW nodes — messages delivered]")
        print(f"  p50={_percentile(gw_delivered, 50):.1f}  p95={_percentile(gw_delivered, 95):.1f}  p99={_percentile(gw_delivered, 99):.1f}  max={max(gw_delivered)}")

        # Chat queue broadcast stats
        bcast_counts = [q.broadcast_count for q in self.chat_queues.values()]
        print("\n[Chat queues — broadcast calls (queue→GW)]")
        print(
            f"  total={sum(bcast_counts):,}  "
            f"p50={_percentile(bcast_counts, 50):.1f}  "
            f"max={max(bcast_counts)}"
        )

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
