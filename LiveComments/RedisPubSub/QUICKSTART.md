# Quick Start Guide

## Run the Simulation

### Step 1: Start the System
Open a terminal and run:
```powershell
docker-compose -f docker-compose-simulator.yml up
```

This will start:
- 5 Reader API instances
- 1 Comment API
- 1 Statistics API
- 1 Redis instance
- 1 Client Simulator (simulating 5000 viewers watching 200 videos)

### Step 2: Watch Statistics
Open a **second terminal** and run:
```powershell
.\watch-stats.ps1
```

This will show you real-time statistics every 10 seconds, including:
- Number of topics each Reader API is subscribed to
- Number of clients connected to each Reader API
- Total comments generated and consumed

### Step 3: Observe the Problem
After a few minutes, you should see:
- **Each Reader API subscribes to ~200 topics** (all videos)
- **Total subscriptions: ~1000** (5 Reader APIs × 200 topics)
- **Optimal would be: ~200** (just the unique videos)

This demonstrates the architectural flaw: each Reader API subscribes to ALL video topics, even though clients are randomly distributed.

## Manual Statistics Query

If you don't want to use the watcher, query manually:
```powershell
curl http://localhost:5000/statistics
```

## Stop the Simulation
Press `Ctrl+C` in the terminal running docker-compose, then:
```powershell
docker-compose -f docker-compose-simulator.yml down
```

## Configuration

To change the scale, edit `docker-compose-simulator.yml`:
```yaml
environment:
  - NUM_VIDEOS=200        # Number of unique videos
  - NUM_VIEWERS=5000      # Number of simulated viewers
  - COMMENT_INTERVAL=300000  # Comment frequency (ms)
```

## What You're Observing

**The Flaw**: With stateless Reader APIs where clients randomly connect:
- Each Reader API subscribes to topics for ALL videos its clients watch
- With random distribution, this approaches ALL videos
- Result: Massive duplication of subscriptions

**Real Numbers**:
- 5000 viewers watching 200 videos
- Random distribution → each Reader API gets clients watching ~200 different videos
- Each Reader API subscribes to ~200 topics
- **Total: 1000 subscriptions instead of 200**

For more details, see [SIMULATION.md](SIMULATION.md)
