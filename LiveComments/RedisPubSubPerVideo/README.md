# Real-Time Comments POC with Redis Pub/Sub

The system design diagram is in the folder.
A proof-of-concept implementation of a real-time commenting system (FB live comments) using Node.js, Redis Pub/Sub, and Server-Sent Events (SSE).

## How It Works

1. **Clients connect** to a Reader API instance via POST `/connect` with their `userid` and `videoid`
2. **Reader API** establishes an SSE connection and subscribes to the Redis topic for that video
3. **Clients post comments** to Comment API via POST `/comment`
4. **Comment API** stores the comment in memory and publishes it to Redis Pub/Sub
5. **All Reader APIs** subscribed to that video topic receive the message and fan it out to connected clients
6. **Clients receive** real-time comment updates via SSE

The objective: Simulate the flaws of this approach in System Designs

## Expected Traffic

The data from Chat GPT:
- Twitch 95k concurrent live streams, 2M concurrent viewers => 25 per video/stream
- Youtube 6M concurrent viewers, no concurrent live streams.
- TikTok, 400k Daily

^^ I would say the fair would be to use 90k concurrent live streams with around 2M viewers.

## Outcome

Each Reader will have a lot of videos, eventually all Reader API instances will be subscribed to 80% of all channels(videos), which makes Reader API horizontal scaling impossible.

## Setup

The project is fully dockerized. Just run:

```bash
docker-compose up
```

## Cleanup

```bash
docker-compose down
```
