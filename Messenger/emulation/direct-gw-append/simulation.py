#!/usr/bin/env python3
"""
Messenger Simulation — Direct GW Append
========================================
Flow per message:
  1. Message arrives at ChatAPI (one per chat).
  2. ChatAPI -> chat-user storage  : get users in the chat.
  3. ChatAPI -> user-gw storage    : resolve which GW node each user is on.
  4. ChatAPI routes to each relevant GW.
  5. GW resolves its local users for the chat (in-memory).
  6. GW resolves socket per user (in-memory).
  7. GW delivers to each socket, retrying on failure.
"""

import hashlib
from collections import defaultdict
from typing import Dict, List, Optional

# ----------------------------------------------------------------
# CONFIGURATION
# ----------------------------------------------------------------

NUM_CHATS            = 3   # also the number of ChatAPI instances
USERS_PER_CHAT       = 4
NUM_GATEWAY_NODES    = 4
NUM_CHAT_USER_SHARDS = 3
NUM_USER_GW_SHARDS   = 4
MAX_DELIVERY_RETRIES = 3

DETAILED_OUTPUT      = True   # False → summary percentile stats only

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
    """
    Simulated WebSocket connection between a User and a GW node.

    A *flaky* socket fails on the very first delivery attempt for each
    unique message, then succeeds on retry — demonstrating retry logic.
    """

    def __init__(self, socket_id: str, user_id: str, flaky: bool = False) -> None:
        self.socket_id = socket_id
        self.user_id   = user_id
        self.flaky     = flaky

        self._attempted: set = set()          # keys of already-tried (chat,msg) pairs
        self.messages_received:       List[dict] = []
        self.total_delivery_attempts: int = 0
        self.failed_attempts:         int = 0
        self.successful_deliveries:   int = 0

    def try_deliver(self, message: str, chat_id: str) -> bool:
        """Push a message through the socket. Returns True on success."""
        self.total_delivery_attempts += 1
        key = f"{chat_id}:{message}"

        if self.flaky and key not in self._attempted:
            self._attempted.add(key)
            self.failed_attempts += 1
            log(
                f"SOCKET {self.socket_id}",
                f"FAILED delivery to {self.user_id} — flaky socket, retrying…",
                level=4,
            )
            return False

        self.messages_received.append({"chat_id": chat_id, "message": message})
        self.successful_deliveries += 1
        log(
            f"SOCKET {self.socket_id}",
            f"OK Delivered to {self.user_id}: '{message}' in {chat_id}",
            level=4,
        )
        return True

    def __repr__(self) -> str:
        flag = " [FLAKY]" if self.flaky else ""
        return f"Socket({self.socket_id}, user={self.user_id}{flag})"


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
# CHAT-USER STORAGE — sharded by chatId
# ----------------------------------------------------------------

class ChatUserStorage:
    """Persistent store: chatId -> [userId].  Sharded by chatId."""

    def __init__(self, num_shards: int) -> None:
        self.num_shards  = num_shards
        self._shards: List[Dict[str, List[str]]] = [{} for _ in range(num_shards)]
        self.query_count = 0
        self.write_count = 0

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
        result = self._shards[s].get(chat_id, [])
        log("ChatUserStorage", f"get_users({chat_id}) -> shard {s} -> {result}", level=2)
        return result


# ----------------------------------------------------------------
# USER-GW STORAGE — sharded by userId
# ----------------------------------------------------------------

class UserGWStorage:
    """Persistent store: userId -> gwId.  Sharded by userId."""

    def __init__(self, num_shards: int) -> None:
        self.num_shards  = num_shards
        self._shards: List[Dict[str, str]] = [{} for _ in range(num_shards)]
        self.query_count = 0
        self.write_count = 0
        self._shard_query_counts: List[int] = [0] * num_shards

    def _shard(self, user_id: str) -> int:
        return int(hashlib.md5(user_id.encode()).hexdigest(), 16) % self.num_shards

    def set_gw(self, user_id: str, gw_id: str) -> None:
        s = self._shard(user_id)
        self.write_count += 1
        self._shards[s][user_id] = gw_id

    def get_gw(self, user_id: str) -> Optional[str]:
        s = self._shard(user_id)
        self.query_count += 1
        self._shard_query_counts[s] += 1
        result = self._shards[s].get(user_id)
        log("UserGWStorage", f"get_gw({user_id}) -> shard {s} -> {result}", level=2)
        return result

    def get_gws_for_users(self, user_ids: List[str]) -> Dict[str, Optional[str]]:
        return {uid: self.get_gw(uid) for uid in user_ids}


# ----------------------------------------------------------------
# GATEWAY NODE
# ----------------------------------------------------------------

class GatewayNode:
    """
    In-memory state (populated at connection time, never queried from storage):
      user_socket : userId -> Socket
      chat_users  : chatId -> [userId]  ← only users connected to THIS GW

    When ChatAPI calls deliver_chat_message(), the GW:
      1. Looks up its local users for the chat (chat_users).
      2. Resolves the socket for each user (user_socket).
      3. Pushes with retry up to MAX_DELIVERY_RETRIES.
    """

    def __init__(self, gw_id: str) -> None:
        self.gw_id = gw_id
        self.user_socket: Dict[str, Socket]    = {}
        self.chat_users:  Dict[str, List[str]] = {}

        # metrics
        self.total_connections  = 0
        self.messages_received  = 0
        self.messages_delivered = 0
        self.retry_count        = 0

    @property
    def active_connections(self) -> int:
        return len(self.user_socket)

    def connect_user(self, user: User, socket: Socket, chat_ids: List[str]) -> None:
        """Register a user's WebSocket and update the in-memory chat->users map."""
        self.user_socket[user.user_id] = socket
        self.total_connections += 1
        for chat_id in chat_ids:
            bucket = self.chat_users.setdefault(chat_id, [])
            if user.user_id not in bucket:
                bucket.append(user.user_id)
        log(
            f"GW {self.gw_id}",
            f"User {user.user_id} connected via {socket.socket_id}, chats={chat_ids}",
            level=1,
        )

    def deliver_chat_message(self, chat_id: str, message: str) -> None:
        """Entry point called by ChatAPI."""
        log(
            f"GW {self.gw_id}",
            f"> deliver_chat_message: chat={chat_id}, msg='{message}'",
            level=1,
        )
        self.messages_received += 1

        # ① Resolve local users for this chat from in-memory state
        users_here = self.chat_users.get(chat_id, [])
        log(f"GW {self.gw_id}", f"in-memory chat_users[{chat_id}] = {users_here}", level=2)

        for user_id in users_here:
            # ② Resolve socket from in-memory state
            socket = self.user_socket.get(user_id)
            if socket is None:
                log(f"GW {self.gw_id}", f"WARNING: no socket found for {user_id}", level=2)
                continue

            log(f"GW {self.gw_id}", f"Resolving socket for {user_id} -> {socket.socket_id}", level=2)

            # ③ Deliver with retry
            delivered = False
            for attempt in range(1, MAX_DELIVERY_RETRIES + 1):
                if attempt > 1:
                    self.retry_count += 1
                    log(f"GW {self.gw_id}", f"<- Retry #{attempt - 1} for {user_id}", level=3)
                if socket.try_deliver(message, chat_id):
                    self.messages_delivered += 1
                    delivered = True
                    break

            if not delivered:
                log(
                    f"GW {self.gw_id}",
                    f"FAIL FAILED to deliver to {user_id} after {MAX_DELIVERY_RETRIES} attempts",
                    level=2,
                )


# ----------------------------------------------------------------
# CHAT API — one instance per chat
# ----------------------------------------------------------------

class ChatAPI:
    """
    Owns a single chat.  On each incoming message:
      1. Queries chat-user storage -> list of users in this chat.
      2. Queries user-gw storage  -> GW node for every user.
      3. Groups users by GW and sends one deliver call per GW.
    """

    def __init__(
        self,
        api_id:            str,
        chat_id:           str,
        chat_user_storage: ChatUserStorage,
        user_gw_storage:   UserGWStorage,
        gw_nodes:          Dict[str, GatewayNode],
    ) -> None:
        self.api_id            = api_id
        self.chat_id           = chat_id
        self.chat_user_storage = chat_user_storage
        self.user_gw_storage   = user_gw_storage
        self.gw_nodes          = gw_nodes

        self.messages_received = 0
        self.gw_route_calls    = 0

    def send_message(self, sender_id: str, message: str) -> None:
        log(
            f"ChatAPI {self.api_id}",
            f"> message from {sender_id}: '{message}'  (chat={self.chat_id})",
        )
        self.messages_received += 1

        # ① Get users in this chat
        users = self.chat_user_storage.get_users(self.chat_id)
        log(f"ChatAPI {self.api_id}", f"users in {self.chat_id}: {users}", level=1)

        # ② Get GW node for every user
        user_gw_map = self.user_gw_storage.get_gws_for_users(users)
        log(f"ChatAPI {self.api_id}", f"user->GW map: {user_gw_map}", level=1)

        # ③ Group by GW and route
        gw_to_users: Dict[str, List[str]] = defaultdict(list)
        for uid, gw_id in user_gw_map.items():
            if gw_id:
                gw_to_users[gw_id].append(uid)

        for gw_id, uids in gw_to_users.items():
            log(
                f"ChatAPI {self.api_id}",
                f"-> routing to {gw_id}  (users on this GW: {uids})",
                level=1,
            )
            self.gw_route_calls += 1
            self.gw_nodes[gw_id].deliver_chat_message(self.chat_id, message)


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
        num_ugw_shards:    int = NUM_USER_GW_SHARDS,
    ) -> None:
        self.num_chats      = num_chats
        self.users_per_chat = users_per_chat
        self.num_gw_nodes   = num_gw_nodes

        self.chat_user_storage = ChatUserStorage(num_cu_shards)
        self.user_gw_storage   = UserGWStorage(num_ugw_shards)

        self.gw_nodes: Dict[str, GatewayNode] = {
            f"GW{i + 1}": GatewayNode(f"GW{i + 1}")
            for i in range(num_gw_nodes)
        }

        self.users:     Dict[str, User]      = {}
        self.chat_apis: Dict[str, ChatAPI]   = {}
        self.chats:     Dict[str, List[str]] = {}

        self._socket_ctr = 0

    # -- helpers --------------------------------------------------

    def _next_socket(self, user_id: str, flaky: bool) -> Socket:
        self._socket_ctr += 1
        return Socket(f"sock{self._socket_ctr}", user_id, flaky=flaky)

    # -- setup ----------------------------------------------------

    def _setup(self) -> None:
        header("SETUP PHASE")
        gw_ids       = list(self.gw_nodes.keys())
        user_counter = 1

        for chat_idx in range(self.num_chats):
            chat_id  = f"chat{chat_idx + 1}"
            user_ids: List[str] = []

            for u_idx in range(self.users_per_chat):
                user_id  = f"u{user_counter}"
                gw_id    = gw_ids[(user_counter - 1) % self.num_gw_nodes]
                is_flaky = (u_idx == 0)   # first user in each chat has a flaky socket
                user_counter += 1

                socket = self._next_socket(user_id, is_flaky)
                user   = User(user_id)
                user.attach(socket, gw_id)

                # Write to persistent storages
                self.user_gw_storage.set_gw(user_id, gw_id)
                self.chat_user_storage.add_user(chat_id, user_id)

                # Populate GW in-memory state at connect time
                self.gw_nodes[gw_id].connect_user(user, socket, [chat_id])

                self.users[user_id] = user
                user_ids.append(user_id)

            self.chats[chat_id] = user_ids
            self.chat_apis[chat_id] = ChatAPI(
                api_id            = f"API-{chat_id}",
                chat_id           = chat_id,
                chat_user_storage = self.chat_user_storage,
                user_gw_storage   = self.user_gw_storage,
                gw_nodes          = self.gw_nodes,
            )

        # Print setup summary (detailed only)
        if DETAILED_OUTPUT:
            print("\nChats:")
            for cid, uids in self.chats.items():
                print(f"  {cid}: {uids}")

            print("\nGateway connections:")
            for gw_id, gw in self.gw_nodes.items():
                print(f"  {gw_id}: users={list(gw.user_socket.keys())}")
                for cid, uids in gw.chat_users.items():
                    print(f"         chat_users[{cid}]={uids}")

            print("\nFlaky sockets (fail first attempt, succeed on retry):")
            for uid, user in self.users.items():
                if user.socket and user.socket.flaky:
                    print(f"  {user.socket.socket_id} -> {uid}")

    # -- run ------------------------------------------------------

    def run_simulation(self) -> None:
        self._setup()

        header("SIMULATION — SENDING MESSAGES")

        # Deterministic: 2 messages per chat (from user[0] and user[1])
        messages = []
        for chat_id, user_ids in self.chats.items():
            messages.append((chat_id, "Hello everyone!", user_ids[0]))
            messages.append((chat_id, "How are you?",    user_ids[1]))

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

        # 1. Messages delivered per chat API (total and per chat)
        print("\n[Chat API — messages delivered]")
        total_api_msgs = 0
        for chat_id, api in self.chat_apis.items():
            total_api_msgs += api.messages_received
            print(f"  {api.api_id}: {api.messages_received} messages sent")
        print(f"  TOTAL: {total_api_msgs}")

        # 2. Messages processed by every GW node
        print("\n[Gateway Nodes — messages processed]")
        for gw_id, gw in self.gw_nodes.items():
            print(
                f"  {gw_id}: received={gw.messages_received}, "
                f"delivered={gw.messages_delivered}, retries={gw.retry_count}"
            )

        # 3. Queries per user-gw storage shard
        print("\n[UserGW Storage — queries per shard]")
        ugw = self.user_gw_storage
        for i, count in enumerate(ugw._shard_query_counts):
            print(f"  shard {i}: {count} queries")
        print(f"  TOTAL: {ugw.query_count} queries")

        # 4. Messages each chat processed + users in that chat
        print("\n[Per-chat summary]")
        for chat_id, user_ids in self.chats.items():
            api = self.chat_apis[chat_id]
            total_delivered = sum(
                self.users[uid].socket.successful_deliveries
                for uid in user_ids
                if self.users[uid].socket
            )
            print(f"  {chat_id}: {api.messages_received} messages sent, {total_delivered} deliveries")
            for uid in user_ids:
                user = self.users[uid]
                if not user.socket:
                    continue
                s = user.socket
                flaky = " [flaky]" if s.flaky else ""
                print(
                    f"    {uid} ({user.gw_id}){flaky}: "
                    f"delivered={s.successful_deliveries}, failed_attempts={s.failed_attempts}"
                )

    def _print_stats_summary(self) -> None:
        print(f"\n{'=' * 50}")
        print("  SIMULATION SUMMARY")
        print(f"{'=' * 50}")

        # GW node message counts
        gw_received = [gw.messages_received for gw in self.gw_nodes.values()]
        print("\n[GW nodes — messages received]")
        print(f"  p50={_percentile(gw_received, 50):.1f}  p99={_percentile(gw_received, 99):.1f}")

        # Messages emitted per chat (same for all chats by design)
        msgs_per_chat = [api.messages_received for api in self.chat_apis.values()]
        print("\n[Messages emitted per chat]")
        print(f"  {msgs_per_chat[0]} per chat  (all chats: {msgs_per_chat})")

        # Per-user successful deliveries across all chats
        user_deliveries = [
            user.socket.successful_deliveries
            for user in self.users.values()
            if user.socket
        ]
        print("\n[Per-user deliveries (across all chats)]")
        print(f"  p50={_percentile(user_deliveries, 50):.1f}  p99={_percentile(user_deliveries, 99):.1f}")


# ----------------------------------------------------------------
# ENTRY POINT
# ----------------------------------------------------------------

if __name__ == "__main__":
    sim = Simulation(
        num_chats      = NUM_CHATS,
        users_per_chat = USERS_PER_CHAT,
        num_gw_nodes   = NUM_GATEWAY_NODES,
    )
    sim.run_simulation()
