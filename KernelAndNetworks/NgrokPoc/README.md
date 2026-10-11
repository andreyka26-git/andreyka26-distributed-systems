# The simplest possible ngrok

A tunnel that makes a server on your laptop reachable through a public server, in about 90 lines of Python (stdlib only).

## The problem

Your laptop sits behind NAT, so nobody on the internet can open a connection **to** it. But it can open connections **out**. So the agent dials the public server first, and the server reuses the agent's connections in the reverse direction.

## How it works

```
visitor ──► server:8000 (public)            server:9000 (agents) ◄── agent ──► 127.0.0.1:3000
              │ read the Host header               ▲   ▲
              │ visitors[domain].put(visitor)      │   │ 2. "DATA app.localhost"  (new connection)
              └─ 1. send one byte on control ──────┘   │
                 3. pipe(visitor, data connection) ◄───┘    agent: pipe(data connection, local app)
```

1. On start, the agent connects to port 9000 and sends `REGISTER app.localhost`. The server stores `agents["app.localhost"] = that connection`. This is the **control connection**; it stays open.
2. A visitor connects to port 8000. The server reads the `Host:` header to find the domain, parks the visitor in a queue, and sends one byte down the control connection: "you have a visitor".
3. The agent opens a second connection to port 9000 and sends `DATA app.localhost`. This is a **data connection**, one per visitor. The agent also connects to the local app.
4. Both sides now just copy bytes: `visitor <-> server <-> agent <-> local app`. Neither side parses HTTP after this point.

The whole thing is one primitive, `pipe(src, dst)`, used four times.

## Run it

### With Docker (emulates the real network layout)

```
docker compose up --build
```

Then open http://app.localhost:8000/ in a browser, or run `curl.exe http://app.localhost:8000/` (in PowerShell plain `curl` is a different command).

```
you ──► server ◄── agent ──► app
       [ public network ]
                   [ private network ]
```

- `server` is on the public network and publishes port 8000: the only thing your machine can reach.
- `app` is only on the private network (`internal: true`): no published port, no route from your machine or from the server.
- `agent` is on both, like a laptop that can reach the internet and its own localhost.

Prove the isolation: the server cannot even resolve the app, yet the tunnel works.

```
docker compose exec server python -c "import socket; socket.create_connection(('app', 3000))"
```

### Why `app.localhost` and not `localhost`

The domain you give the agent is the **public name visitors type**, not the address the agent dials. The server routes by the `Host:` header, so `http://localhost:8000/` arrives as `Host: localhost`, no agent registered that name, and you get a 502.

In real ngrok, DNS points `*.ngrok.app` at the server's IP, so every domain reaches the same server and the `Host:` header tells them apart. Here `*.localhost` plays that role with zero setup. The `app` part is arbitrary; any `<name>.localhost` works.

### How `app.localhost` is resolved

The name does two separate jobs:

1. **Finding the machine.** `localhost` is a reserved name (RFC 6761): it and everything under it should mean "this machine". Browsers and `curl.exe` hardcode that, so they turn `app.localhost` into `127.0.0.1` without asking DNS and connect to port 8000. The server never sees the name at this step.
2. **Routing inside the server.** The client also writes the name into the request as `Host: app.localhost`. That header is what the server looks up in `agents`.

Windows itself does not resolve `*.localhost`, only plain `localhost`. So clients that rely on the OS resolver (Python, .NET) fail at step 1. For those, do step 1 by hand and supply step 2 yourself:

```
curl.exe -H "Host: app.localhost" http://127.0.0.1:8000/
```

## What real ngrok adds

- TLS on the public side and on the agent connection
- Auth: who is allowed to claim which domain
- One multiplexed connection (yamux) instead of a new data connection per visitor
- Reconnects, heartbeats, timeouts
- Reading the full headers properly (here they must fit in the first `recv`)
