import queue
import socket
import threading
from collections import defaultdict
from contextlib import suppress

PUBLIC_PORT = 8000  # visitors (browsers, curl) connect here
AGENT_PORT = 9000   # agents connect here

# A visitor is someone from the internet (a browser, curl) who connected to the public port.
agent_control_connections = {}                          # domain -> agent's control connection
visitors = defaultdict(queue.Queue)  # domain -> visitors not yet paired with a data connection


def pipe(src, dst):
    """Copy bytes from src to dst until src has nothing more to say."""
    with suppress(OSError):
        while data := src.recv(65536):
            dst.sendall(data)
    with suppress(OSError):
        dst.shutdown(socket.SHUT_WR)  # pass the "end of stream" along


def handle_public(visitor_conn):
    first_chunk = visitor_conn.recv(65536)  # assume all the headers fit in here
    domain = None
    for line in first_chunk.split(b"\r\n"):
        if line.lower().startswith(b"host:"):
            domain = line[5:].decode().strip().split(":")[0]

    if domain not in agent_control_connections:
        visitor_conn.sendall(b"HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n")
        return

    visitors[domain].put((visitor_conn, first_chunk))
    
    # "you have a visitor, open a data connection" by dialing control connection
    agent_control_connections[domain].sendall(b"\n")


def handle_agent(agent_conn):
    """Runs once per connection an agent opens to us, each time in its own thread."""
    # The agent's first message is b"REGISTER app.localhost\n" or b"DATA app.localhost\n"
    kind, domain = agent_conn.recv(1024).decode().split()

    if kind == "REGISTER":  # control connection: stays open as long as the agent lives
        agent_control_connections[domain] = agent_conn
        print("agent registered:", domain)
        # The agent never sends anything else here, so recv only returns when it disconnects.
        # Only this thread waits; visitors and DATA connections run in their own threads.
        with suppress(OSError):
            agent_conn.recv(1)
        del agent_control_connections[domain]
        print("agent gone:", domain)

    if kind == "DATA":  # data connection: glue it to the oldest waiting visitor
        visitor_conn, first_chunk = visitors[domain].get()
        agent_conn.sendall(first_chunk)
        # A connection flows both ways, and one thread can only wait on one socket.
        threading.Thread(target=pipe, args=(visitor_conn, agent_conn)).start()  # request, in a new thread
        pipe(agent_conn, visitor_conn)                                          # response, in this thread


def listen(port, handler):
    """Accept connections on a port forever; run handler(conn) for each in a new thread."""
    server = socket.create_server(("", port))
    while True:
        conn, _ = server.accept()
        threading.Thread(target=handler, args=(conn,), daemon=True).start()


# Two listening ports, and accept() blocks, so one of the two loops needs its own thread.
threading.Thread(target=listen, args=(AGENT_PORT, handle_agent), daemon=True).start()
print(f"public on :{PUBLIC_PORT}, agents on :{AGENT_PORT}")
listen(PUBLIC_PORT, handle_public)
