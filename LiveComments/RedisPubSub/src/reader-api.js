const express = require('express');
const redis = require('redis');
const axios = require('axios');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 4000;
const INSTANCE_ID = process.env.INSTANCE_ID || 'reader-1';
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';

// Track active connections per video
const connections = {};
const subscribedTopics = new Set();

// Statistics tracking
let messagesSent = 0;

let redisSubscriber;

async function initRedis() {
  redisSubscriber = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisSubscriber.on('error', (err) => console.error('Redis Client Error', err));
  await redisSubscriber.connect();
  console.log('Connected to Redis');

  // Handle incoming messages
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
  
  if (!subscribedTopics.has(topic)) {
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
}

// Send statistics to Statistics API
async function sendStatistics() {
  try {
    // Calculate active connections
    let totalConnections = 0;
    for (const videoConnections of Object.values(connections)) {
      totalConnections += videoConnections.length;
    }

    await axios.post(`${STATISTICS_API_URL}/reader-api-statistics`, {
      instanceId: INSTANCE_ID,
      activeConnections: totalConnections,
      subscribedTopics: Array.from(subscribedTopics),
      messagesSent
    });
    console.log(`[${INSTANCE_ID}] 📊 Sent statistics: connections=${totalConnections}, messages=${messagesSent}, topics=${subscribedTopics.size}`);
  } catch (err) {
    console.error(`[${INSTANCE_ID}] Error sending statistics:`, err.message);
  }
}

app.post('/connect', async (req, res) => {
  const { userid, videoid } = req.body;

  if (!userid || !videoid) {
    return res.status(400).json({ error: 'userid and videoid are required' });
  }

  console.log(`[${INSTANCE_ID}] New connection request: user ${userid} for video ${videoid}`);

  // Set up SSE
  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');

  // Send initial connection message
  res.write(`data: ${JSON.stringify({ type: 'connected', userid, videoid, instance: INSTANCE_ID })}\n\n`);

  // Track connection
  if (!connections[videoid]) {
    connections[videoid] = [];
    await subscribeToVideo(videoid);
  }
  connections[videoid].push(res);

  console.log(`[${INSTANCE_ID}] Active connections for video ${videoid}: ${connections[videoid].length}`);

  // Handle client disconnect
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

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`[${INSTANCE_ID}] Reader API listening on port ${PORT}`);
  });

  // Send statistics every 5 seconds
  setInterval(sendStatistics, 5000);

  // Send initial statistics after 3 seconds
  setTimeout(sendStatistics, 3000);
}

start();
