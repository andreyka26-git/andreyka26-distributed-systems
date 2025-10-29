const express = require('express');
const redis = require('redis');
const axios = require('axios');
const { StatisticsUtils } = require('../utils');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 4000;
const INSTANCE_ID = process.env.INSTANCE_ID || 'reader-1';
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';

const connections = {};
const subscribedTopics = new Set();

let messagesSent = 0;

let redisSubscriber;

async function initRedis() {
  redisSubscriber = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisSubscriber.on('error', (err) => console.error('Redis Client Error', err));
  await redisSubscriber.connect();
  console.log('Connected to Redis');

  redisSubscriber.on('message', (channel, message) => {
    console.log(`[${INSTANCE_ID}] Received message on ${channel}`);
    const videoId = channel.replace('video:', '');
    
    if (connections[videoId]) {
      connections[videoId].forEach(client => {
        client.write(`data: ${message}\n\n`);
      });
      console.log(`[${INSTANCE_ID}] Sent to ${connections[videoId].length} clients watching video ${videoId}`);
    }
  });
}

async function subscribeToVideo(videoid) {
  const topic = `video:${videoid}`;
  
  if (subscribedTopics.has(topic)) {
    return;
  }

  await redisSubscriber.subscribe(topic, (message) => {
    console.log(`[${INSTANCE_ID}] Message on ${topic}: ${message}`);
    
    if (connections[videoid]) {
      connections[videoid].forEach(client => {
        client.write(`data: ${message}\n\n`);
        messagesSent++;
      });
      console.log(`[${INSTANCE_ID}] Sent to ${connections[videoid].length} clients`);
    }
  });
  
  subscribedTopics.add(topic);
  console.log(`[${INSTANCE_ID}] Subscribed to ${topic}`);
}

app.post('/connect', async (req, res) => {
  const { userid, videoid } = req.body;

  if (!userid || !videoid) {
    return res.status(400).json({ error: 'userid and videoid are required' });
  }

  console.log(`[${INSTANCE_ID}] New connection request: user ${userid} for video ${videoid}`);

  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');

  res.write(`data: ${JSON.stringify({ type: 'connected', userid, videoid, instance: INSTANCE_ID })}\n\n`);

  // Initialize connections array first to avoid race condition
  if (!connections[videoid]) {
    connections[videoid] = [];
  }
  
  connections[videoid].push(res);

  // Subscribe to topic after adding connection
  if (connections[videoid].length === 1) {
    await subscribeToVideo(videoid);
  }

  console.log(`[${INSTANCE_ID}] Active connections for video ${videoid}: ${connections[videoid].length}`);

  req.on('close', () => {
    console.log(`[${INSTANCE_ID}] Client disconnected: user ${userid} from video ${videoid}`);
    if (connections[videoid]) {
      connections[videoid] = connections[videoid].filter(client => client !== res);
      
      if (connections[videoid].length === 0) {
        delete connections[videoid];
        console.log(`[${INSTANCE_ID}] No more connections for video ${videoid}`);
      }
    }
  });
});

app.get('/health', (req, res) => {
  res.json({ 
    status: 'ok', 
    service: 'reader-api',
    instance: INSTANCE_ID,
    activeVideos: Object.keys(connections).length,
    subscribedTopics: Array.from(subscribedTopics)
  });
});

async function sendStatistics() {
  try {
    const totalConnections = Object.values(connections).reduce((sum, conns) => sum + conns.length, 0);

    await StatisticsUtils.sendStatistics(
      STATISTICS_API_URL,
      '/reader-api-statistics',
      {
        instanceId: INSTANCE_ID,
        activeConnections: totalConnections,
        subscribedTopics: Array.from(subscribedTopics),
        messagesSent
      },
      `[${INSTANCE_ID}]`
    );
  } catch (err) {
    console.error(`[${INSTANCE_ID}] Error sending statistics:`, err.message);
  }
}

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`[${INSTANCE_ID}] Reader API listening on port ${PORT}`);
  });

  setInterval(sendStatistics, 5000);
  setTimeout(sendStatistics, 3000);
}

start();
