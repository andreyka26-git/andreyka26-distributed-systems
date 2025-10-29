# Real-Time Comments POC with Redis Pub/Sub and Stateful Video Assignment

A proof-of-concept implementation of a real-time commenting system (FB live comments) using Node.js, Redis Pub/Sub, Server-Sent Events (SSE), and a centralized ReaderApiManager for stateful video-to-reader mapping.

## How It Works

The system uses a stateful architecture where each video is assigned to exactly one Reader API instance:

1. **ReaderApiManager** maintains a persistent mapping of `{videoid => readerApiUrl}` in Redis
2. **Client** queries ReaderApiManager to resolve which Reader API handles their video
3. **Client** connects to the assigned Reader API and subscribes to real-time updates
4. **Reader API** subscribes to Redis Pub/Sub channel for videos that have active connections
5. **Comment API** publishes comments to Redis channels and also registers videos with ReaderApiManager
6. **Reader API** fans out messages to all connected clients for that video

## Key Architecture Changes from Stateless Version

- **ReaderApiManager**: New centralized service that assigns videos to reader APIs using round-robin
- **Stateful Assignment**: Each video is always handled by the same Reader API instance
- **Client Resolution**: Clients must query ReaderApiManager before connecting to a Reader API
- **Persistent Mappings**: Video-to-reader assignments are stored in Redis and survive restarts

## Expected Traffic

The data from Chat GPT:
- Twitch 95k concurrent live streams, 2M concurrent viewers => 25 per video/stream
- Youtube 6M concurrent viewers, no concurrent live streams.
- TikTok, 400k Daily

^^ I would say the fair would be to use 90k concurrent live streams with around 2M viewers.

## Architecture

- **ReaderApiManager** (1 instance): Manages video-to-reader API mappings using round-robin assignment
- **Comment API** (1 instance, can be scaled by replication as stateless): Receives new comments and publishes them to Redis Pub/Sub
- **Reader API** (3 instances, stateful, hard to scale): Manages SSE connections and subscribes to Redis topics for real-time updates
- **Clients** (multiple instances): Simulate users watching videos and posting comments
- **Redis**: Pub/Sub message broker and mapping storage
- **Statistics API** (1 instance): Collects and aggregates statistics from all services

## Benefits of Stateful Architecture

1. **Predictable Assignment**: Each video always maps to the same Reader API
2. **Perfect Video Balance**: Round-robin ensures equal video distribution across readers
3. **Simplified Client Logic**: Clients query once to find their Reader API
4. **Persistent Mappings**: Assignments survive restarts and are cached in Redis

## Trade-offs

1. **Viewer Load Imbalance**: Popular videos can create hotspots on specific readers
2. **Single Point of Failure**: ReaderApiManager becomes a critical component
3. **Less Dynamic**: Cannot redistribute load based on real-time viewer counts
4. **Additional Complexity**: Need to maintain and query video-to-reader mappings

## How It Works

1. **Comment Posted**: Client posts comment to Comment API, which registers video with ReaderApiManager
2. **Video Registration**: ReaderApiManager assigns video to a Reader API using round-robin (if not already assigned)
3. **Client Connection**: Client queries ReaderApiManager to resolve which Reader API handles their video
4. **Reader Connection**: Client connects to assigned Reader API via POST `/connect` with `userid` and `videoid`
5. **Topic Subscription**: Reader API subscribes to Redis Pub/Sub topic for that video
6. **Message Fan-out**: When comments arrive, Reader API delivers them to all connected clients via SSE

## Setup

The project is fully dockerized. Just run:

```bash
docker-compose up
```

## Configuration

The setup includes:
- **ReaderApiManager** (port 6000): Manages video-to-reader API mappings
- **Comment API** (port 3000): Receives comments and registers videos with ReaderApiManager
- **Reader API instances** (ports 4001, 4002): Handle SSE connections for assigned videos
- **Clients**: Multiple clients watching different videos and posting comments

## Testing

Watch the logs to see:
- Clients querying ReaderApiManager to resolve reader APIs
- Videos being assigned to specific Reader API instances
- Comments being posted and published to Redis
- Real-time delivery of comments to watching clients

```bash
docker-compose logs -f
```

## API Endpoints

### ReaderApiManager (port 6000)
- `POST /register` - Register/resolve reader API for a video
  ```json
  {
    "videoid": "video1"
  }
  ```
- `GET /resolve/:videoid` - Get reader API URL for a video
- `GET /health` - Health check

### Statistics API (port 5000)
- `GET /statistics` - **Get all system statistics in one endpoint**
  - Returns: Comment API stats, Reader API stats, Client stats, ReaderApiManager stats
- `POST /comment-api-statistics` - Submit Comment API statistics (internal use)
- `POST /reader-api-statistics` - Submit Reader API statistics (internal use)
- `POST /reader-api-manager-statistics` - Submit ReaderApiManager statistics (internal use)
- `POST /client-statistics` - Submit client statistics (internal use)
- `GET /health` - Health check

### Comment API (port 3000)
- `POST /comment` - Submit a new comment (also registers video with ReaderApiManager)
  ```json
  {
    "userid": "user1",
    "videoid": "video1", 
    "comment": "Great video!"
  }
  ```
- `GET /statistics` - Proxies to Statistics API `/statistics`
- `GET /health` - Health check

### Reader API (ports 4001, 4002)
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
# Get all statistics (comment API, reader APIs, clients, ReaderApiManager)
curl http://localhost:5000/statistics

# Or via Comment API proxy
curl http://localhost:3000/statistics

# Check ReaderApiManager health
curl http://localhost:6000/health

# Resolve reader API for a specific video
curl http://localhost:6000/resolve/video1
```

## Simulation

Run the small-scale simulation to see how videos are distributed across Reader API instances:

```bash
# From project directory
node simulation/small_in_memory_simulation.js
```

This shows how the stateful architecture distributes 100 videos with 2,500 viewers across 5 Reader API instances using round-robin assignment.

## Cleanup

```bash
docker-compose down
```
