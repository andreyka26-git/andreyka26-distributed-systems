# The simplest possible ngrok, deployed for real

The same tunnel as [../NgrokPoc](../NgrokPoc/README.md), split the way you would actually run it:

- `server/` runs on a public machine, shipped as a Docker image.
- `agent/` runs on your laptop with plain Python (stdlib only, nothing to install).

```
visitor ──► server:80 (public)              server:9000 (agents) ◄── agent ──► 127.0.0.1:3000
            [ public machine, Docker ]                               [ your laptop, Python ]
```

How the tunnel itself works is explained in [../NgrokPoc/README.md](../NgrokPoc/README.md). `server.py` is identical; the agent only differs in taking the server address, the domain and the local port as arguments instead of hardcoding them.

## 1. Build and push the server image

Replace `REGISTRY/USER` with your registry path (for Docker Hub just your username).

```
docker login
docker build -t REGISTRY/USER/ngrok-server:latest server
docker push REGISTRY/USER/ngrok-server:latest
```

If your laptop and the server have different CPU architectures (for example an ARM server), add `--platform linux/arm64` to the build.

## 2. Run it on the server

```
docker run -d --name ngrok-server --init --restart unless-stopped \
  -p 80:8000 -p 9000:9000 \
  REGISTRY/USER/ngrok-server:latest
```

- `80 -> 8000` is the public port visitors hit.
- `9000` is where agents dial in.

Both ports must be open in the server's firewall / cloud security group. Check it with `docker logs ngrok-server`.

## 3. Point a domain at the server

The server routes by the `Host:` header, so every name you want to tunnel must resolve to the server's IP. One wildcard DNS record covers all of them:

```
*.example.com   A   <server IP>
```

No domain yet? Skip this step and pass the `Host:` header by hand (see step 5).

## 4. Run the agent on your laptop

Start something to expose:

```
cd app
python -m http.server 3000
```

Then, in another terminal, dial the server:

```
python agent/agent.py <server IP or hostname> app.example.com 3000
```

The arguments are: where the server is, the public name visitors will type, and the local port to forward to.

## 5. Visit it

Open http://app.example.com/ in a browser, or without DNS:

```
curl.exe -H "Host: app.example.com" http://<server IP>/
```

(In PowerShell plain `curl` is a different command.)

## Before you leave it running

This is still the teaching version, now on the open internet:

- **No auth.** Anyone who can reach port 9000 can register any domain, including one you are using, and receive its visitors. Restrict port 9000 to your own IP in the firewall.
- **No TLS.** Visitor traffic and the agent connection are plain text.
- **No reconnect.** If the connection drops, restart the agent.
