# The simplest possible ngrok

A tunnel that makes a server on your laptop reachable through a public server, in about 140 lines of Python (stdlib only).

## The problem

Your laptop sits behind NAT, so nobody on the internet can open a connection **to** it. But it can open connections **out**. So the agent dials the public server first, and the server reuses the agent's connections in the reverse direction.

## How it works

```
visitor ──► server:8000 (public)            server:9000 (agents) ◄── agent ──► 127.0.0.1:3000
              │ read the app name from the path    ▲   ▲
              │ visitors[app].put(visitor)         │   │ 2. "DATA app1"  (new connection)
              └─ 1. send one byte on control ──────┘   │
                 3. pipe(visitor, data connection) ◄───┘    agent: pipe(data connection, local app)
```

1. On start, the agent connects to port 9000 and sends `REGISTER app1`. The server stores `agent_control_connections["app1"] = that connection` and answers `OK`. This is the **control connection**; it stays open. If the name is already held by another agent the server answers `NO` and the agent exits: names are unique.
2. A visitor opens `http://<server>:8000/app1/css/a.css`. The server takes the first path segment (`app1`) as the app name, parks the visitor in a queue, and sends one byte down the control connection: "you have a visitor".
3. The agent opens a second connection to port 9000 and sends `DATA app1`. This is a **data connection**, one per visitor. The agent also connects to the local app.
4. Both sides now just copy bytes: `visitor <-> server <-> agent <-> local app`.

The whole thing is one primitive, `pipe(src, dst)`, used four times.

### What the server does to the request

The local app knows nothing about the tunnel, so the server edits the first chunk before passing it on:

- **Cuts the app name off the path.** `GET /app1/css/a.css` becomes `GET /css/a.css`.
- **Redirects `/app1` to `/app1/`.** Without the trailing slash, a relative link like `style.css` on the page would resolve to `/style.css` and lose the app name.
- **Sets `Connection: close`.** Only the first chunk of a connection is rewritten, and a connection is glued to one app. Closing after each response makes every request arrive on a fresh connection and get routed and rewritten again.

## Run it

### With Docker (emulates the real network layout)

```
docker compose up --build
```

Then open http://localhost:8000/app1/ in a browser, or run `curl.exe http://localhost:8000/app1/` (in PowerShell plain `curl` is a different command).

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

### Why real ngrok routes by domain, not by path

Path routing needs no DNS, which is why it is used here. Its price: the app is served under `/app1/` while believing it lives at `/`. Relative links (`style.css`) work; absolute ones (`/style.css`, a redirect to `/login`) leave the prefix and get a 404 from the server.

Real ngrok gives every app its own domain (`abc123.ngrok.app`), all pointing at the same server, and routes by the `Host:` header. The path is then passed through untouched, so any app works unchanged.

## What real ngrok adds

- TLS on the public side and on the agent connection
- A domain per app instead of a path prefix
- Auth: who is allowed to claim which name
- One multiplexed connection (yamux) instead of a new data connection per visitor
- Reconnects, heartbeats, timeouts
- Reading the full headers properly (here they must fit in the first `recv`)
