import socket
import sys
import threading
from contextlib import suppress

# python agent.py tunnel.example.com app.example.com 3000
SERVER_HOST = sys.argv[1]      # the public ngrok server we dial
DOMAIN = sys.argv[2]           # the public name visitors will use; the server routes by it
LOCAL_PORT = int(sys.argv[3])  # where the local app listens

SERVER = (SERVER_HOST, 9000)       # the ngrok server we dial (its agent port)
LOCAL = ("127.0.0.1", LOCAL_PORT)  # the app we expose


def pipe(src, dst):
    """Copy bytes from src to dst until src has nothing more to say."""
    with suppress(OSError):
        while data := src.recv(65536):
            dst.sendall(data)
    with suppress(OSError):
        dst.shutdown(socket.SHUT_WR)  # pass the "end of stream" along


# Control plane connection: we dial OUT, so it works from behind NAT or a firewall.
# used to signal there is incoming data plane connection
control_connection = socket.create_connection(SERVER)
control_connection.sendall(f"REGISTER {DOMAIN}\n".encode())
print(f"http://{DOMAIN} -> http://{LOCAL[0]}:{LOCAL[1]}")

# the server sends one byte per visitor
while control_connection.recv(1):
    # data plane connection
    dataplane_connection = socket.create_connection(SERVER)
    dataplane_connection.sendall(f"DATA {DOMAIN}\n".encode())

    local = socket.create_connection(LOCAL)

    # A connection flows both ways, and one thread can only wait on one socket.
    threading.Thread(target=pipe, args=(dataplane_connection, local), daemon=True).start()  # request
    threading.Thread(target=pipe, args=(local, dataplane_connection), daemon=True).start()  # response
