const express = require('express');
const redis = require('redis');
const axios = require('axios');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 6000;
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';

// Available reader API instances
const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://localhost:4001', 'http://localhost:4002', 'http://localhost:4003'];

let redisClient;
let registrationCount = 0;

async function initRedis() {
  redisClient = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisClient.on('error', (err) => console.error('Redis Client Error', err));
  await redisClient.connect();
  console.log('ReaderApiManager connected to Redis');
}

// POST endpoint for registering a reader API to a video
app.post('/register', async (req, res) => {
  const { videoid } = req.body;

  if (!videoid) {
    return res.status(400).json({ error: 'videoid is required' });
  }

  const redisKey = `video:${videoid}:reader`;

  try {
    // Check if there's already a reader API assigned to this video
    const existingReaderUrl = await redisClient.get(redisKey);
    
    if (existingReaderUrl) {
      console.log(`[ReaderApiManager] Video ${videoid} already has reader API: ${existingReaderUrl}`);
      return res.json({ 
        videoid, 
        readerUrl: existingReaderUrl,
        action: 'existing'
      });
    }

    // Select a reader API instance (round-robin based on registration count)
    const selectedReaderUrl = READER_API_URLS[registrationCount % READER_API_URLS.length];
    registrationCount++;

    // Store the mapping in Redis
    await redisClient.set(redisKey, selectedReaderUrl);
    
    console.log(`[ReaderApiManager] Registered video ${videoid} to reader API: ${selectedReaderUrl}`);
    
    res.json({ 
      videoid, 
      readerUrl: selectedReaderUrl,
      action: 'registered'
    });

  } catch (err) {
    console.error(`[ReaderApiManager] Error registering video ${videoid}:`, err.message);
    res.status(500).json({ error: 'Internal server error' });
  }
});

// GET endpoint for resolving reader API URL by video ID
app.get('/resolve/:videoid', async (req, res) => {
  const { videoid } = req.params;

  if (!videoid) {
    return res.status(400).json({ error: 'videoid is required' });
  }

  const redisKey = `video:${videoid}:reader`;

  try {
    const readerUrl = await redisClient.get(redisKey);
    
    if (!readerUrl) {
      return res.status(404).json({ 
        error: 'No reader API registered for this video',
        videoid 
      });
    }

    console.log(`[ReaderApiManager] Resolved video ${videoid} to reader API: ${readerUrl}`);
    
    res.json({ 
      videoid, 
      readerUrl 
    });

  } catch (err) {
    console.error(`[ReaderApiManager] Error resolving video ${videoid}:`, err.message);
    res.status(500).json({ error: 'Internal server error' });
  }
});

app.get('/health', (req, res) => {
  res.json({ 
    status: 'ok', 
    service: 'reader-api-manager',
    availableReaderApis: READER_API_URLS.length,
    registrationCount
  });
});

async function sendStatistics() {
  try {
    // Get all registered video-to-reader mappings
    const keys = await redisClient.keys('video:*:reader');
    const registeredVideos = keys.length;

    await axios.post(`${STATISTICS_API_URL}/reader-api-manager-statistics`, {
      service: 'reader-api-manager',
      registeredVideos,
      availableReaderApis: READER_API_URLS.length,
      registrationCount
    });
    console.log(`[ReaderApiManager] Sent statistics: registeredVideos=${registeredVideos}, availableReaderApis=${READER_API_URLS.length}`);
  } catch (err) {
    console.error(`[ReaderApiManager] Error sending statistics:`, err.message);
  }
}

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`[ReaderApiManager] Reader API Manager listening on port ${PORT}`);
    console.log(`[ReaderApiManager] Available Reader APIs: ${READER_API_URLS.join(', ')}`);
  });

  setInterval(sendStatistics, 5000);
  setTimeout(sendStatistics, 3000);
}

start();