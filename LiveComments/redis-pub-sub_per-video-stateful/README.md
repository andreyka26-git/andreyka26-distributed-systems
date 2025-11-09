# Real-Time Comments POC with Redis Pub/Sub and Stateful Video Assignment

A proof-of-concept implementation of a real-time commenting system (FB live comments) using Node.js, Redis Pub/Sub, Server-Sent Events (SSE), and a centralized ReaderApiManager for stateful video-to-reader mapping.

## How It Works

The system uses a stateful architecture where each video is assigned to exactly one Reader API instance:

1. **ReaderApiManager** maintains a persistent mapping of `{videoid => readerApiUrl}` in Redis
2. **Client** queries ReaderApiManager to resolve which Reader API handles their video and connects to the assigned Reader API and subscribes to real-time updates
4. **Reader API** subscribes to Redis Pub/Sub channel for videos that have active connections, and fans out messages to all connected clients for that video
5. **Comment API** publishes comments to Redis channels and also registers videos with ReaderApiManager

## Expected Traffic

The data from Chat GPT:
- Twitch 95k concurrent live streams, 2M concurrent viewers => 25 per video/stream
- Youtube 6M concurrent viewers, no concurrent live streams.
- TikTok, 400k Daily

^^ I would say the fair would be to use 90k concurrent live streams with around 2M viewers.

## Outcome

Now each reader handles pretty much even number of videos therefore load (celebrity problem is out of scope). But there is no point to keep Redis PubSub at the moment, as Comment API can resolve the reader url from ReaderApiManager and push message directly there.


# To Run

```bash
docker-compose up
```

## Cleanup

```bash
docker-compose down
```
