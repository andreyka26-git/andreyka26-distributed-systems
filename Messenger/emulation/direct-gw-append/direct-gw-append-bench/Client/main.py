#!/usr/bin/env python3
"""
Benchmark Client — direct-gw-append-bench
==========================================
Emulates N chats, each with M users connected via WebSocket to a Gateway.
For every chat a "ChatAPI" loop fires HTTP POST /chat/{chatId}/message at the
configured rate for a fixed duration.

Each user listens on its WebSocket and records the end-to-end latency
(message payload carries sentAt epoch-ms; receivedAt is measured locally).

At the end the client prints per-chat send counts, per-user received counts,
latency percentiles, and fetches the Gateway's own internal stats.

Usage
-----
python main.py [options]

Options
-------
  --gw-url      http://localhost:5000   Base URL of the Gateway
  --chats       3                       Number of independent chats
  --users       4                       Users per chat
  --rate        10                      Messages per second PER chat
  --duration    30                      How long to run (seconds)
"""

import argparse
import asyncio
import json
import time
from collections import defaultdict
from typing import Dict, List

import aiohttp


# ----------------------------------------------------------------
# Default configuration  — edit these to change benchmark defaults
# ----------------------------------------------------------------

GW_URL   = "http://test1.andreyka26.com" # Base URL of the Gateway
CHATS    = 1
USERS    = 4
RATE     = 1.0 # Messages per second per chat
DURATION = 5.0 # Benchmark duration (seconds)


# ----------------------------------------------------------------
# Shared state  (written from async tasks, read at the end)
# ----------------------------------------------------------------

# userId -> list[latency_ms]
user_latencies: Dict[str, List[float]] = defaultdict(list)

# chatId -> int  (messages successfully POSTed)
messages_sent: Dict[str, int] = defaultdict(int)


# ----------------------------------------------------------------
# WebSocket listener — one coroutine per user
# ----------------------------------------------------------------

async def user_listener(
    session: aiohttp.ClientSession,
    user_id: str,
    chat_id: str,
    gw_ws_url: str,
    stop_event: asyncio.Event,
    connected_event: asyncio.Event,
) -> None:
    """Connect to the Gateway WebSocket and record received messages."""
    url = f"{gw_ws_url}/ws?userId={user_id}&chatId={chat_id}"
    try:
        async with session.ws_connect(url, heartbeat=30) as ws:
            connected_event.set()
            while not stop_event.is_set():
                try:
                    msg = await asyncio.wait_for(ws.receive(), timeout=1.0)
                except asyncio.TimeoutError:
                    continue

                if msg.type == aiohttp.WSMsgType.TEXT:
                    received_at = time.time() * 1000.0          # epoch ms
                    try:
                        data = json.loads(msg.data)
                        sent_at = float(data.get("sentAt", received_at))
                        user_latencies[user_id].append(received_at - sent_at)
                    except (json.JSONDecodeError, ValueError):
                        pass
                elif msg.type in (aiohttp.WSMsgType.CLOSED, aiohttp.WSMsgType.ERROR):
                    break
    except Exception as exc:
        print(f"  [WS {user_id}] error: {exc}")


# ----------------------------------------------------------------
# ChatAPI sender — one coroutine per chat
# ----------------------------------------------------------------

async def chat_sender(
    session:  aiohttp.ClientSession,
    chat_id:  str,
    sender_id: str,
    gw_http_url: str,
    rate:     float,
    duration: float,
) -> None:
    """Send messages at `rate` msg/s for `duration` seconds."""
    interval  = 1.0 / rate
    end_time  = time.monotonic() + duration
    seq       = 0

    while time.monotonic() < end_time:
        sent_at = int(time.time() * 1000)
        payload = {
            "senderId": sender_id,
            "message":  f"msg-{seq}",
            "sentAt":   sent_at,
        }
        try:
            async with session.post(
                f"{gw_http_url}/chat/{chat_id}/message",
                json=payload,
                timeout=aiohttp.ClientTimeout(total=5),
            ) as resp:
                if resp.status == 200:
                    messages_sent[chat_id] += 1
                    seq += 1
        except Exception as exc:
            print(f"  [ChatAPI {chat_id}] send error: {exc}")

        await asyncio.sleep(interval)


# ----------------------------------------------------------------
# Stats helpers
# ----------------------------------------------------------------

async def fetch_stats(session: aiohttp.ClientSession, gw_http: str) -> dict | None:
    try:
        async with session.get(
            f"{gw_http}/stats",
            timeout=aiohttp.ClientTimeout(total=5),
        ) as resp:
            return await resp.json()
    except Exception as exc:
        print(f"Could not fetch /stats from gateway: {exc}")
        return None


async def reset_stats(session: aiohttp.ClientSession, gw_http: str) -> None:
    try:
        async with session.post(
            f"{gw_http}/stats/reset",
            timeout=aiohttp.ClientTimeout(total=5),
        ) as resp:
            if resp.status == 200:
                print("Gateway stats reset.")
            else:
                print(f"Stats reset returned unexpected status {resp.status}")
    except Exception as exc:
        print(f"Could not reset /stats on gateway: {exc}")


def percentile(data: List[float], p: float) -> float:
    if not data:
        return 0.0
    s = sorted(data)
    idx = p / 100.0 * (len(s) - 1)
    lo  = int(idx)
    hi  = min(lo + 1, len(s) - 1)
    return s[lo] + (idx - lo) * (s[hi] - s[lo])


def print_gw_stats(gw_stats: dict | None, label: str = "Gateway stats") -> None:
    if not gw_stats:
        print(f"\n[{label} — unavailable]")
        return

    lat  = gw_stats.get("deliveryLatency", {})
    chat = gw_stats.get("chat", {})
    upc  = chat.get("usersPerChat", {})
    mpc  = chat.get("messagesPerChat", {})

    print(f"\n[{label}]")
    print("  Delivery latency (POST receipt → all WS sends done):")
    print(
        f"    operations={lat.get('operationsPerformed', 'N/A')}"
        f"  p50={lat.get('p50Ms', 0):.1f}ms"
        f"  p99={lat.get('p99Ms', 0):.1f}ms"
        f"  min={lat.get('minMs', 0):.1f}ms"
        f"  max={lat.get('maxMs', 0):.1f}ms"
    )
    print("  Chat topology:")
    print(
        f"    totalChats={chat.get('totalChats', 'N/A')}"
        f"  usersPerChat p50={upc.get('p50', 0):.0f}"
        f"  p99={upc.get('p99', 0):.0f}"
    )
    print("  Messages processed:")
    print(
        f"    total={chat.get('totalMessagesProcessed', 'N/A')}"
        f"  perChat p50={mpc.get('p50', 0):.0f}"
        f"  p99={mpc.get('p99', 0):.0f}"
    )


def print_stats(chats: Dict[str, List[str]], gw_stats: dict | None) -> None:
    sep = "=" * 62

    print(f"\n{sep}")
    print("  BENCHMARK RESULTS")
    print(sep)

    # --- sent per chat ---
    print("\n[Messages sent per chat (via HTTP POST)]")
    total_sent = 0
    for chat_id in sorted(chats):
        n = messages_sent[chat_id]
        total_sent += n
        print(f"  {chat_id}: {n} messages sent")
    print(f"  TOTAL: {total_sent}")

    # --- per-user received + latency ---
    print("\n[Per-user received messages & end-to-end latency]")
    all_latencies: List[float] = []
    for chat_id, users in sorted(chats.items()):
        print(f"\n  {chat_id}  (sent={messages_sent[chat_id]}):")
        for uid in users:
            lats = user_latencies[uid]
            all_latencies.extend(lats)
            if lats:
                p50 = percentile(lats, 50)
                p99 = percentile(lats, 99)
                print(f"    {uid}: received={len(lats):4d}  p50={p50:6.1f}ms  p99={p99:6.1f}ms")
            else:
                print(f"    {uid}: received=   0  (no messages received)")

    # --- overall latency ---
    if all_latencies:
        print(f"\n[Overall end-to-end latency (all users, all chats)]")
        print(
            f"  count={len(all_latencies)}"
            f"  p50={percentile(all_latencies, 50):.1f}ms"
            f"  p99={percentile(all_latencies, 99):.1f}ms"
        )

    print_gw_stats(gw_stats, label="Gateway internal stats (this run)")


# ----------------------------------------------------------------
# Entry point
# ----------------------------------------------------------------

async def main() -> None:
    parser = argparse.ArgumentParser(description="Gateway benchmark client")
    parser.add_argument("--gw-url",   default=GW_URL,
                        help=f"Base HTTP URL of the Gateway  (default: {GW_URL})")
    parser.add_argument("--chats",    type=int,   default=CHATS,
                        help=f"Number of independent chats  (default: {CHATS})")
    parser.add_argument("--users",    type=int,   default=USERS,
                        help=f"Users per chat               (default: {USERS})")
    parser.add_argument("--rate",     type=float, default=RATE,
                        help=f"Messages per second per chat (default: {RATE})")
    parser.add_argument("--duration", type=float, default=DURATION,
                        help=f"Benchmark duration seconds   (default: {DURATION})")
    args = parser.parse_args()

    gw_http = args.gw_url.rstrip("/")
    gw_ws   = gw_http.replace("http://", "ws://")

    # Build chat -> [userId] mapping.
    # Each user belongs to exactly one chat.
    chats: Dict[str, List[str]] = {}
    for c in range(args.chats):
        chat_id = f"chat{c + 1}"
        users   = [f"u{c * args.users + u + 1}" for u in range(args.users)]
        chats[chat_id] = users

    total_users = args.chats * args.users
    print(f"Gateway      : {gw_http}")
    print(f"Chats        : {args.chats}  x  {args.users} users  =  {total_users} total connections")
    print(f"Send rate    : {args.rate} msg/s per chat  ({args.chats * args.rate:.1f} msg/s total)")
    print(f"Duration     : {args.duration}s")
    print()

    stop_event = asyncio.Event()

    connector = aiohttp.TCPConnector(limit=0)          # unlimited connections
    async with aiohttp.ClientSession(connector=connector) as session:

        # 1. Connect all users via WebSocket
        print("Connecting users…")
        connected_events: Dict[str, asyncio.Event] = {}
        ws_tasks = []
        for chat_id, users in chats.items():
            for uid in users:
                ev = asyncio.Event()
                connected_events[uid] = ev
                ws_tasks.append(
                    asyncio.create_task(
                        user_listener(session, uid, chat_id, gw_ws, stop_event, ev)
                    )
                )

        # Wait up to 5s for every user to confirm a successful WebSocket handshake.
        connect_timeout = 5.0
        wait_tasks = [
            asyncio.create_task(ev.wait())
            for ev in connected_events.values()
        ]
        _, pending = await asyncio.wait(wait_tasks, timeout=connect_timeout)

        failed_users = [
            uid for uid, ev in connected_events.items() if not ev.is_set()
        ]
        if failed_users:
            print(f"\nERROR: {len(failed_users)} user(s) failed to connect: {failed_users}")
            print("Aborting benchmark — fix the WebSocket connectivity issue first.")
            stop_event.set()
            for t in pending:
                t.cancel()
            await asyncio.gather(*ws_tasks, return_exceptions=True)
            return

        print(f"All {total_users} users connected.")

        # 2. Fetch and display any leftover stats from a previous run, then reset.
        pre_run_stats = await fetch_stats(session, gw_http)
        print_gw_stats(pre_run_stats, label="Gateway stats before this run (previous run leftover)")
        await reset_stats(session, gw_http)
        print()

        print("Starting senders…\n")

        # 3. Start one ChatAPI sender per chat — all fire simultaneously
        sender_tasks = [
            asyncio.create_task(
                chat_sender(session, chat_id, users[0], gw_http, args.rate, args.duration)
            )
            for chat_id, users in chats.items()
        ]

        # 4. Wait for all senders to finish
        await asyncio.gather(*sender_tasks)
        print("\nAll senders done.  Waiting for in-flight messages…")

        # Grace period: let the last WebSocket frames arrive
        await asyncio.sleep(2.0)

        # 5. Signal WebSocket listeners to stop and wait
        stop_event.set()
        await asyncio.gather(*ws_tasks, return_exceptions=True)

        # 6. Fetch Gateway's final stats for this run
        gw_stats = await fetch_stats(session, gw_http)

    print_stats(chats, gw_stats)


if __name__ == "__main__":
    asyncio.run(main())
