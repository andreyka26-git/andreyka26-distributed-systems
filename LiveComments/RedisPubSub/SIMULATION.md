# Scale Simulation Guide

## Objective
Demonstrate the key flaw in Redis Pub/Sub approach: **Every Reader API instance subscribes to ALL video topics**, regardless of which clients are connected to it.

## The Problem

When users randomly connect to Reader API instances:
- **Reader API 1** might get clients watching videos: 1, 5, 12, 45, 89...
- **Reader API 2** might get clients watching videos: 3, 5, 15, 47, 92...
- **Reader API 3** might get clients watching videos: 2, 5, 18, 51, 95...

Each Reader API must subscribe to Redis Pub/Sub topics for ALL videos its clients are watching. With random distribution across 200 videos:
- **Each Reader API will subscribe to ~200 topics** (nearly all of them)
- **Total subscriptions = 200 topics × 5 Reader APIs = 1000 subscriptions**

This is the core scalability problem being demonstrated.

## New Efficient Simulation Approach

Instead of creating thousands of Docker containers, we use a single **client simulator** that:
- Runs multiple client instances as in-memory objects (classes)
- Manages 5000 viewers in a single process
- Distributes clients across 5 Reader API instances
- Uses minimal resources

## Quick Start

### Full Scale (200 videos, 5000 viewers) - RECOMMENDED
```bash
# Terminal 1: Start the system
docker-compose -f docker-compose-simulator.yml up

# Terminal 2: Watch statistics (PowerShell)
.\watch-stats.ps1

# Or manually query once:
curl http://localhost:5000/statistics
```

This creates only **8 containers**:
- 1 Redis
- 1 Statistics API
- 1 Comment API
- 5 Reader APIs
- 1 Client Simulator (simulating 5000 viewers)

### Custom Scale
Edit `docker-compose-simulator.yml` and change the simulator environment variables:

```yaml
environment:
  - NUM_VIDEOS=50      # Number of unique videos
  - NUM_VIEWERS=1000   # Number of viewers
  - COMMENT_INTERVAL=300000  # Time between comments (ms)
```

Then run:
```bash
docker-compose -f docker-compose-simulator.yml up
```

## Key Metrics to Observe

### Via Statistics API (Recommended)

**Live monitoring with the stats watcher:**
```powershell
.\watch-stats.ps1
```

**One-time query:**
```powershell
# PowerShell: Pretty print
curl http://localhost:5000/statistics | ConvertFrom-Json | ConvertTo-Json -Depth 10

# PowerShell: Table view of Reader APIs
curl http://localhost:5000/statistics | ConvertFrom-Json | Select-Object -ExpandProperty readerApis | Select-Object -ExpandProperty readers | ForEach-Object { 
    [PSCustomObject]@{
        Instance = $_.instanceId
        Topics = $_.subscribedTopics.Count
        Connections = $_.activeConnections
        MessagesSent = $_.messagesSent
    }
} | Format-Table
```

**What to look for:**
1. **`subscribedTopics.Count` per Reader API**: Should approach 200 (total number of videos)
2. **`activeConnections` per Reader API**: Should be ~1000 per instance (5000 / 5)
3. **Total subscriptions**: ~1000 (5 Reader APIs × 200 topics each)
4. **Client distribution**: How the 5000 clients are spread across the 5 Reader APIs

### Via Logs
```bash
# Watch topic subscriptions
docker-compose -f docker-compose-scale.yml logs reader-api-1 | grep "Subscribed to"

# Count topics per reader
docker-compose -f docker-compose-scale.yml logs reader-api-1 | grep "Subscribed to" | wc -l
```

## Expected Results (200 videos, 5000 viewers, 5 Reader APIs)

```json
{
  "readerApis": [
    {
      "instanceId": "reader-api-1",
      "subscribedTopics": "~200 topics (all of them)",
      "activeConnections": "~1000 clients"
    },
    {
      "instanceId": "reader-api-2",
      "subscribedTopics": "~200 topics (all of them)",
      "activeConnections": "~1000 clients"
    },
    {
      "instanceId": "reader-api-3",
      "subscribedTopics": "~200 topics (all of them)",
      "activeConnections": "~1000 clients"
    },
    {
      "instanceId": "reader-api-4",
      "subscribedTopics": "~200 topics (all of them)",
      "activeConnections": "~1000 clients"
    },
    {
      "instanceId": "reader-api-5",
      "subscribedTopics": "~200 topics (all of them)",
      "activeConnections": "~1000 clients"
    }
  ],
  "totalRedisSubscriptions": "~1000 (5 Reader APIs × 200 topics)",
  "problem": "Each Reader API receives ALL messages for ALL videos, even though each only serves ~1000 clients watching a subset of videos"
}
```

## Why This is a Problem

1. **Network Overhead**: Each Reader API receives messages for ALL 200 videos, but only needs messages for the videos its clients are watching
2. **Processing Waste**: With 5000 clients spread across 200 videos, most clients watch popular videos. Less popular videos have few viewers, but ALL Reader APIs still subscribe to those topics.
3. **Doesn't Scale**: Adding more Reader APIs = more wasted subscriptions (1000 → 1200 → 1400...)
4. **Memory Growth**: Each Reader API tracks 200 topic subscriptions regardless of actual need

## Comparison: What a Good System Would Do

A properly designed system would:
- **Reader API 1**: Subscribe ONLY to topics for videos its clients watch (e.g., ~40 unique videos)
- **Reader API 2**: Subscribe ONLY to topics for videos its clients watch (e.g., ~45 unique videos)
- **Reader API 3**: Subscribe ONLY to topics for videos its clients watch (e.g., ~38 unique videos)
- **Reader API 4**: Subscribe ONLY to topics for videos its clients watch (e.g., ~42 unique videos)
- **Reader API 5**: Subscribe ONLY to topics for videos its clients watch (e.g., ~35 unique videos)
- **Total subscriptions**: ~200 instead of 1000

But this is **impossible with stateless Reader APIs** where clients can connect to any instance randomly.

## Infrequent Comments Configuration

The simulation uses infrequent comments (default: 300 seconds = 5 minutes) to:
- Focus on **subscription patterns** rather than message throughput
- Reduce noise in logs
- Keep resource usage manageable
- Make it easier to observe the architecture problem

You can change comment frequency by editing `COMMENT_INTERVAL` in `generate-docker-compose.js`.

## Cleanup

```bash
docker-compose -f docker-compose-scale.yml down
```

## Analysis Commands

```bash
# Get full statistics
curl http://localhost:5000/statistics

# PowerShell: Pretty print statistics
curl http://localhost:5000/statistics | ConvertFrom-Json | ConvertTo-Json -Depth 10

# See topic count per Reader API
curl http://localhost:5000/statistics | ConvertFrom-Json | Select-Object -ExpandProperty readerApis | ForEach-Object { 
    [PSCustomObject]@{
        Instance = $_.instanceId
        Topics = $_.subscribedTopics.Count
        Connections = $_.activeConnections
    }
}

# Watch simulator logs
docker-compose -f docker-compose-simulator.yml logs -f client-simulator

# Watch Reader API subscriptions
docker-compose -f docker-compose-simulator.yml logs reader-api-1 | Select-String "Subscribed to"
```

## Video Distribution

The simulator uses a **Zipf-like distribution** to model realistic viewership:
- **video1**: ~850 viewers (most popular)
- **video2**: ~425 viewers
- **video3**: ~283 viewers
- ...
- **video200**: ~1 viewer (least popular)

This models real-world scenarios where a few videos are very popular and many are niche content.
