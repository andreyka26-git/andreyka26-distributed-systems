import queue
import re
import socket
import threading
from collections import defaultdict
from contextlib import suppress

PUBLIC_PORT = 8000  # visitors (browsers, curl) connect here
AGENT_PORT = 9000   # agents connect here

# A visitor is someone from the internet (a browser, curl) who connected to the public port.
agent_control_connections = {}       # app name -> agent's control connection
visitors = defaultdict(queue.Queue)  # app name -> visitors not yet paired with a data connection

# b"GET /app1/css/a.css HTTP/1.1" -> method b"GET", app name b"app1", slash b"/"
REQUEST_START = re.compile(rb"(\w+) /([\w-]+)(/?)")


def pipe(src, dst):
    """Copy bytes from src to dst until src has nothing more to say."""
    with suppress(OSError):
        while data := src.recv(65536):
            dst.sendall(data)
    with suppress(OSError):
        dst.shutdown(socket.SHUT_WR)  # pass the "end of stream" along


def respond(visitor_conn, status, extra_headers=""):
    """Answer the visitor ourselves, without involving any agent."""
    visitor_conn.sendall(f"HTTP/1.1 {status}\r\n{extra_headers}Content-Length: 0\r\nConnection: close\r\n\r\n".encode())
    visitor_conn.close()


def handle_public(visitor_conn):
    first_chunk = visitor_conn.recv(65536)  # assume all the headers fit in here
    match = REQUEST_START.match(first_chunk)
    app = match[2].decode() if match else None

    if app not in agent_control_connections:
        respond(visitor_conn, "404 Not Found")
        return

    if not match[3]:  # "/app1" -> "/app1/", so that relative links on the page stay under /app1/
        respond(visitor_conn, "301 Moved Permanently", f"Location: /{app}/\r\n")
        return

    # The local app knows nothing about the tunnel: cut the app name off, "GET /app1/css/a.css" -> "GET /css/a.css".
    # Only this first chunk is rewritten, so ask the app to close after one response:
    # the browser's next request then comes on a new connection and through here again.
    request_line, _, headers = first_chunk[match.end():].partition(b"\r\n")
    headers = re.sub(rb"(?im)^connection:.*\r\n", b"", headers, count=1)
    first_chunk = match[1] + b" /" + request_line + b"\r\nConnection: close\r\n" + headers

    visitors[app].put((visitor_conn, first_chunk))

    # "you have a visitor, open a data connection" by dialing control connection
    agent_control_connections[app].sendall(b"\n")


def handle_agent(agent_conn):
    """Runs once per connection an agent opens to us, each time in its own thread."""
    # The agent's first message is b"REGISTER app1\n" or b"DATA app1\n"
    kind, app = agent_conn.recv(1024).decode().split()

    if kind == "REGISTER":  # control connection: stays open as long as the agent lives
        # Names are unique: setdefault stores ours only if nobody holds the name yet.
        if agent_control_connections.setdefault(app, agent_conn) is not agent_conn:
            agent_conn.sendall(b"NO\n")
            agent_conn.close()
            print("agent rejected, name is taken:", app)
            return
        agent_conn.sendall(b"OK\n")
        print("agent registered:", app)
        # The agent never sends anything else here, so recv only returns when it disconnects.
        # Only this thread waits; visitors and DATA connections run in their own threads.
        with suppress(OSError):
            agent_conn.recv(1)
        del agent_control_connections[app]
        print("agent gone:", app)

    if kind == "DATA":  # data connection: glue it to the oldest waiting visitor
        visitor_conn, first_chunk = visitors[app].get()
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
