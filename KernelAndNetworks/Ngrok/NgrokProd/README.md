# The simplest possible ngrok, deployed for real

The same tunnel as [../NgrokPoc](../NgrokPoc/README.md), split the way you would actually run it:

- `server/` runs on a public machine, shipped as a Docker image.
- `agent/` runs on your laptop with plain Python (stdlib only, nothing to install).

```
visitor ──► server:8000 (public)            server:9000 (agents) ◄── agent ──► 127.0.0.1:3000
            [ public machine, Docker ]                               [ your laptop, Python ]
```

How the tunnel itself works is explained in [../NgrokPoc/README.md](../NgrokPoc/README.md). `server.py` is identical; the agent only differs in taking the server address, the app name and the local port as arguments instead of hardcoding them.

## 1. Build and push the server image (on your laptop)

The image lives in GitHub Container Registry as `ghcr.io/andreyka26-git/ngrok-server:latest`.

> **Using this repo yourself?** You cannot push to that path. Change the `image:` line in both [docker-compose.yml](docker-compose.yml) and [docker-compose.prod.yml](docker-compose.prod.yml) to your own registry path, for example `ghcr.io/<your-github-user>/ngrok-server:latest` (or `<your-dockerhub-user>/ngrok-server:latest` for Docker Hub), and use your own username in the `docker login` commands below.

Log in once. The password is a GitHub personal access token with the `write:packages` scope, not your GitHub password. Paste it at the prompt; never put it in a file in this repo.

```
docker login ghcr.io -u andreyka26-git
```

Then, from this folder (`NgrokProd`):

```
docker compose build
docker compose push
```

That uses [docker-compose.yml](docker-compose.yml). To try the image locally before pushing, run `docker compose up` (public side on port 8000, agents on 9000).

If your laptop and the server have different CPU architectures (for example an ARM server), add `platform: linux/arm64` under `ngrok-server:` in `docker-compose.yml` before building.

## 2. Pull and run it on the server

Copy [docker-compose.prod.yml](docker-compose.prod.yml) to the server and name it `docker-compose.yml`:

```
mkdir ngrok-server && cd ngrok-server
nano docker-compose.yml      # paste the contents of docker-compose.prod.yml
```

A new GHCR package is private. Either make it public (GitHub -> your profile -> Packages -> ngrok-server -> Package settings -> Change visibility), or log in on the server with a token that has the `read:packages` scope:

```
docker login ghcr.io -u andreyka26-git
```

Then pull and start:

```
docker compose pull
docker compose up -d
```

- `8000` is the public port visitors hit. If nothing else on the server owns port 80, change the mapping to `80:8000` and drop the `:8000` from the URLs below.
- `9000` is where agents dial in.

Both ports must be open in the server's firewall / cloud security group. Check it with `docker compose logs -f`.

To ship a new version: `docker compose build` and `docker compose push` on the laptop, then `docker compose pull` and `docker compose up -d` on the server.

## 3. Run the agent on your laptop

Start something to expose:

```
cd app
python -m http.server 3000
```

Then, in another terminal, dial the server and claim a name:

```
python agent/agent.py <server IP> app1 3000
```

The arguments are: where the server is, the app name (letters, digits, `_` and `-`), and the local port to forward to. The agent prints the public URL. If another agent already holds the name, the server refuses it and the agent exits.

## 4. Visit it

Open `http://<server IP>:8000/app1/` in a browser. The server cuts the app name off, so `http://<server IP>:8000/app1/some/page` reaches `http://127.0.0.1:3000/some/page` on your laptop.

Port 9000 is for agents only; opening it in a browser does nothing useful.

## Several apps at once

One server tunnels many apps, each to its own agent. Run one agent per app, each with a different name:

```
python agent/agent.py <server IP> app1 3000
python agent/agent.py <server IP> app2 4000
```

`http://<server IP>:8000/app1/` and `http://<server IP>:8000/app2/` now reach two different local ports, possibly on two different laptops.

## Before you leave it running

This is still the teaching version, now on the open internet:

- **No auth.** Anyone who can reach port 9000 can register any free name and serve whatever they like under your server's address. Restrict port 9000 to your own IP in the firewall.
- **No TLS.** Visitor traffic and the agent connection are plain text.
- **No reconnect.** If the connection drops, restart the agent.
