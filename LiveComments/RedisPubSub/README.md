# Real-Time Comments POC with Redis Pub/Sub

Bad solution in case there are too many videos. Too heavy videos (celebrity problem) is out of scope here.
Since users are going to random "reader" service. In the end it can happen, that every reader API can be overwhelmed with so many comments processed by redis publisher.

A proof-of-concept implementation of a real-time commenting system (FB live comments) using Node.js, Redis Pub/Sub, and Server-Sent Events (SSE).

## Architecture

- **Comment API** (1 instance, can be scaled by replication as stateless): Receives new comments and publishes them to Redis Pub/Sub
- **Reader API** (3 instances, stateful, hard to scale): Manages SSE connections and subscribes to Redis topics for real-time updates
- **Clients** (3 instances): Simulate users watching videos and posting comments
- **Redis**: Pub/Sub message broker
- **Statistics API** (1 instance): Collects and aggregates statistics from all services

## How It Works

1. **Clients connect** to a Reader API instance via POST `/connect` with their `userid` and `videoid`
2. **Reader API** establishes an SSE connection and subscribes to the Redis topic for that video
3. **Clients post comments** to Comment API via POST `/comment`
4. **Comment API** stores the comment in memory and publishes it to Redis Pub/Sub
5. **All Reader APIs** subscribed to that video topic receive the message and fan it out to connected clients
6. **Clients receive** real-time comment updates via SSE

## Setup

The project is fully dockerized. Just run:

```bash
docker-compose up
```

## Configuration

The setup includes:
- **client-1**: user1 watching video1
- **client-2**: user2 watching video2  
- **client-3**: user3 watching video1

This means:
- Redis will have 2 topics: `video:video1` and `video:video2`
- Reader APIs will subscribe to both topics
- Comments on video1 will be delivered to client-1 and client-3
- Comments on video2 will be delivered to client-2

## Testing

Watch the logs to see:
- Clients connecting to different Reader API instances
- Comments being posted and published to Redis
- Real-time delivery of comments to watching clients

```bash
docker-compose logs -f
```

## API Endpoints

### Statistics API (port 5000)
- `GET /statistics` - **Get all system statistics in one endpoint**
  - Returns: Comment API stats, Reader API stats, Client stats - everything in one response
- `POST /comment-api-statistics` - Submit Comment API statistics (internal use)
- `POST /reader-api-statistics` - Submit Reader API statistics (internal use)
- `POST /client-statistics` - Submit client statistics (internal use)
- `GET /health` - Health check

### Comment API (port 3000)
- `POST /comment` - Submit a new comment
  ```json
  {
    "userid": "user1",
    "videoid": "video1", 
    "comment": "Great video!"
  }
  ```
- `GET /statistics` - Proxies to Statistics API `/statistics`
- `GET /health` - Health check

### Reader API (ports 4001, 4002, 4003)
- `POST /connect` - Establish SSE connection
  ```json
  {
    "userid": "user1",
    "videoid": "video1"
  }
  ```
- `GET /health` - Health check and status

## Getting Statistics

All system statistics are available at a single endpoint:

```bash
# Get all statistics (comment API, reader APIs, clients)
curl http://localhost:5000/statistics

# Or via Comment API proxy
curl http://localhost:3000/statistics
```

## Cleanup

```bash
docker-compose down
```
