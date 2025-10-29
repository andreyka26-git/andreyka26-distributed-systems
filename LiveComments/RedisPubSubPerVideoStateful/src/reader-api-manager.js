const express = require('express');
const redis = require('redis');
const axios = require('axios');
const { StatisticsUtils } = require('./utils');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 6000;
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';

const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://localhost:4001', 'http://localhost:4002', 'http://localhost:4003'];

let redisClient;
let registrationCount = 0;

async function initRedis() {
  redisClient = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisClient.on('error', (err) => console.error('Redis Client Error', err));
  await redisClient.connect();
}

app.post('/register', async (req, res) => {
  const { videoid } = req.body;

  if (!videoid) {
    return res.status(400).json({ error: 'videoid is required' });
  }

  const redisKey = `video:${videoid}:reader`;

  try {
    const existingReaderUrl = await redisClient.get(redisKey);
    
    if (existingReaderUrl) {
      console.log(`[ReaderApiManager] Video ${videoid} already has reader API: ${existingReaderUrl}`);
      return res.json({ 
        videoid, 
        readerUrl: existingReaderUrl,
        action: 'existing'
      });
    }

    const selectedReaderUrl = READER_API_URLS[registrationCount % READER_API_URLS.length];
    registrationCount++;

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
    
    res.json({ 
      videoid, 
      readerUrl 
    });

  } catch (err) {
    console.error(`[ReaderApiManager] Error resolving video ${videoid}:`, err.message);
    res.status(500).json({ error: 'Internal server error' });
  }
});

async function sendStatistics() {
  try {
    // Get all registered video-to-reader mappings
    const keys = await redisClient.keys('video:*:reader');
    const registeredVideos = keys.length;

    // Get the actual mappings for the top 10 latest registrations
    const mappings = {};
    if (keys.length > 0) {
      // Sort keys to get the most recent ones (keys are in format video:videoid:reader)
      const sortedKeys = keys.sort().slice(-10); // Get last 10 (most recent)
      
      for (const key of sortedKeys) {
        const videoid = key.match(/video:(.+):reader/)[1];
        const readerUrl = await redisClient.get(key);
        mappings[videoid] = readerUrl;
      }
    }

    await StatisticsUtils.sendStatistics(
      STATISTICS_API_URL,
      '/reader-api-manager-statistics',
      {
        service: 'reader-api-manager',
        registeredVideos,
        availableReaderApis: READER_API_URLS.length,
        registrationCount,
        videoMappings: mappings
      },
      '[ReaderApiManager]'
    );
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