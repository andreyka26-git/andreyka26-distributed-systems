#!/usr/bin/env python3
"""Two clients, one point: what a SILENT connection costs the server you point this at.

Both servers listen on port 9000, and this dials port 9000. Run one server at a time
(they cannot share the port), then just:

    python demo_clients.py

Both connections are open at the same time, for the whole run:

  client A - "silent"     connects, echoes one message, then never speaks again.
                          It sends no bytes and it does not disconnect; the server is
                          left holding a connection that will never become readable.
  client B - "heartbeat"  connects second, then echoes one message every 5 seconds.

B is the measuring stick. Its round trips are flat and boring on a server that can
serve two connections at once, and they are not on a server that cannot:

  - thread-per-connection blocking server: B is served - but look at the server log, a
    SECOND thread with a second tid, created solely because A might one day say
    something.
  - single-threaded blocking server: B is served by NOBODY. Its connection finished the
    TCP handshake (the kernel parked it in the accept queue) but the server's only
    thread is asleep inside read() on A and never reaches accept() again. Every beat
    sits unanswered until A disconnects - then all of them come back at once. That
    stall is the drawback.
  - readiness server (epoll / kqueue / WSAPoll): B is served on the beat, on the SAME
    tid that served A. A silent connection is one registration that stops coming back
    ready; nothing is parked on it.

By default A disconnects after 3 beats, so you can watch what its departure releases.
Pass --hold-beats >= --beats and it never disconnects at all.

Usage: demo_clients.py [--port P] [--host H] [--beats N] [--interval S]
                       [--hold-beats N] [--idle N]

--idle N additionally holds N connections that do nothing at all, so you can count what
an idle connection costs the server - a thread, or one entry in a table.
"""
import argparse
import socket
import sys
import time

# The one port both servers bind. Keep this in step with kPort in blocking_server.cpp
# and epoll_server.cpp - they are deliberately the same, so this script never has to
# know or care which of the two you started.
DEFAULT_PORT = 9000

START = time.monotonic()


def log(tag, msg):
    print("[%7.2fs] %-9s %s" % (time.monotonic() - START, tag, msg))


def connect(host, port, tag=None):
    """Open one connection, or explain why we could not and give up."""
    try:
        s = socket.create_connection((host, port), timeout=10)
    except OSError as e:
        print("cannot connect to %s:%d - %s" % (host, port, e))
        print("Start blocking_server or epoll_server in another terminal first - "
              "either one, they both listen on %d." % port)
        sys.exit(1)
    # Nagle would delay these tiny messages and blur the timings we are here to read.
    s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    if tag:
        log(tag, "connected to %s:%d (local port %d)" % (host, port, s.getsockname()[1]))
    return s


def drain(sock, buf, deadline, on_line):
    """Read whatever arrives until `deadline`, handing complete lines to on_line().

    Returns the leftover buffer, or None if the peer closed."""
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return buf
        sock.settimeout(remaining)
        try:
            chunk = sock.recv(65536)
        except socket.timeout:
            return buf
        except OSError as e:
            log("heartbeat", "connection error: %s" % e)
            return None
        if not chunk:
            return None
        buf += chunk
        while b"\n" in buf:
            line, buf = buf.split(b"\n", 1)
            on_line(line.decode("utf-8", "replace"))


def main():
    ap = argparse.ArgumentParser(
        description="Hold one silent connection and one heartbeat connection against "
                    "whichever echo server is running. Both listen on port 9000.")
    ap.add_argument("--port", type=int, default=DEFAULT_PORT,
                    help="default %d - the port both servers listen on" % DEFAULT_PORT)
    ap.add_argument("--host", default="127.0.0.1", help="default 127.0.0.1")
    ap.add_argument("--beats", type=int, default=6, help="heartbeat messages to send")
    ap.add_argument("--interval", type=float, default=5.0, help="seconds between beats")
    ap.add_argument("--hold-beats", type=int, default=3,
                    help="beats to stay silent for before client A disconnects; pass a "
                         "value >= --beats and it never disconnects")
    ap.add_argument("--idle", type=int, default=0, metavar="N",
                    help="hold N extra silent connections open, to count what an idle "
                         "connection costs the server (a thread, or a registration)")
    args = ap.parse_args()

    where = "%s:%d" % (args.host, args.port)
    leaves = ("the silent client leaves after beat %d" % args.hold_beats
              if args.hold_beats < args.beats else "the silent client never leaves")
    print("talking to %s - %d beats, one every %.1fs; %s\n"
          % (where, args.beats, args.interval, leaves))

    # ---- client A: one echo, then silence -----------------------------------------
    silent = connect(args.host, args.port, "silent")
    silent.sendall(b"echo1\n")
    log("silent", "sent 'echo1'")
    silent.settimeout(10)
    try:
        reply = silent.recv(4096)
    except socket.timeout:
        reply = b""
    log("silent", "got %r back - so this connection IS being served" % reply)
    log("silent", "going quiet now. No more bytes, no disconnect: a blocking read() on "
                  "this socket will simply never return.")

    # ---- more of the same, if asked: N connections that only ever exist -------------
    idle = []
    for _ in range(args.idle):
        idle.append(connect(args.host, args.port))
    if idle:
        log("idle", "%d extra silent connections held open - now count the server's "
                    "threads (see README, Diagnostics)" % len(idle))

    # ---- client B: a beat every `interval` seconds ---------------------------------
    beat = connect(args.host, args.port, "heartbeat")
    sent_at = {}      # beat number -> send time
    replied = {}      # beat number -> round trip seconds
    buf = b""
    dead = False

    def on_line(line):
        n = line.strip().rsplit("#", 1)[-1]
        if not n.isdigit() or int(n) not in sent_at:
            log("heartbeat", "unexpected reply %r" % line)
            return
        k = int(n)
        rtt = time.monotonic() - sent_at[k]
        replied[k] = rtt
        late = "  <-- stalled, the server could not get to it sooner" \
            if rtt > args.interval else ""
        log("heartbeat", "beat %d: echo came back after %.3fs%s" % (k, rtt, late))

    for k in range(1, args.beats + 1):
        if k == args.hold_beats + 1 and silent is not None:
            log("silent", "disconnecting - FIN. Whatever was parked on this socket is "
                          "released now; watch what the heartbeat does next.")
            silent.close()
            silent = None

        if not dead:
            try:
                beat.sendall(("echo2 #%d\n" % k).encode())
                sent_at[k] = time.monotonic()
                log("heartbeat", "beat %d: sent 'echo2 #%d'" % (k, k))
            except OSError as e:
                log("heartbeat", "beat %d: send failed: %s" % (k, e))
                dead = True

        # Wait out the interval, printing replies as they land instead of at the end.
        deadline = time.monotonic() + args.interval
        if not dead:
            buf = drain(beat, buf, deadline, on_line)
            if buf is None:
                log("heartbeat", "server closed the connection")
                dead = True
        if k in sent_at and k not in replied:
            log("heartbeat", "beat %d: NO REPLY after %.1fs - nobody is reading this "
                             "socket" % (k, args.interval))
        while time.monotonic() < deadline:
            time.sleep(0.05)

    # One last window for replies that were queued behind the silent connection.
    if not dead:
        drain(beat, buf, time.monotonic() + 2.0, on_line)
    beat.close()
    if silent is not None:
        silent.close()
    for s_ in idle:
        s_.close()

    # ---- verdict --------------------------------------------------------------------
    worst = max(replied.values()) if replied else 0.0
    missed = [k for k in sent_at if k not in replied]
    print("\n--- %s ---" % where)
    print("beats sent %d, answered %d, unanswered %d" %
          (len(sent_at), len(replied), len(missed)))
    print("worst round trip: %.3fs" % worst)
    if missed:
        print("VERDICT: the silent connection starved the heartbeat outright - those "
              "beats were never echoed.")
    elif worst > args.interval:
        print("VERDICT: the silent connection BLOCKED the heartbeat. The delayed beats "
              "were answered only once client A disconnected.")
    else:
        print("VERDICT: the silent connection cost the heartbeat nothing - this server "
              "served both at once. Check its log for how many distinct tids that took.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
