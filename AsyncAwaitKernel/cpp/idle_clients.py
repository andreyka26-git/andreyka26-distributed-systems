#!/usr/bin/env python3
"""Open N idle TCP connections and hold them, to compare what an idle connection
costs in each server.

  blocking_server (port 9001): N sleeping threads  -> check `ls /proc/<pid>/task | wc -l`
  epoll_server    (port 9002): N epitems, 1 thread -> the task count stays at 1

Usage: python3 idle_clients.py [port] [count]
"""
import socket
import sys
import time

port = int(sys.argv[1]) if len(sys.argv) > 1 else 9001
count = int(sys.argv[2]) if len(sys.argv) > 2 else 500

socks = []
for i in range(count):
    s = socket.create_connection(("127.0.0.1", port))
    socks.append(s)
    if (i + 1) % 100 == 0:
        print(f"{i + 1} connections open")

print(f"{len(socks)} idle connections held. Ctrl-C to drop them.")
try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    pass
