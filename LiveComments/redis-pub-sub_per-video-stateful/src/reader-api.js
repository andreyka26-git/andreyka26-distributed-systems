const express = require('express');
const redis = require('redis');
const axios = require('axios');
const { StatisticsUtils } = require('./utils');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 4000;
const INSTANCE_ID = process.env.INSTANCE_ID || 'reader-1';
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';
const READER_API_MANAGER_URL = process.env.READER_API_MANAGER_URL || 'http://localhost:6000';

const connections = {};
const subscribedTopics = new Set();

let messagesSent = 0;

let redisSubscriber;

async function initRedis() {
  redisSubscriber = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisSubscriber.on('error', (err) => console.error('Redis Client Error', err));
  await redisSubscriber.connect();

  redisSubscriber.on('message', (channel, message) => {
    const videoId = channel.replace('video:', '');
    
    if (connections[videoId]) {
      connections[videoId].forEach(client => {
        client.write(`data: ${message}\n\n`);
      });
    }
  });
}

async function subscribeToVideo(videoid) {
  const topic = `video:${videoid}`;
  
  if (subscribedTopics.has(topic)) {
    return;
  }

  const response = await axios.get(`${READER_API_MANAGER_URL}/resolve/${videoid}`);
  const assignedReaderUrl = response.data.readerUrl;
  
  if (!assignedReaderUrl.includes(INSTANCE_ID)) {
    console.log(`[${INSTANCE_ID}] Error, video ${videoid} is assigned to another reader: ${assignedReaderUrl}`);
    return;
  }

  await redisSubscriber.subscribe(topic, (message) => {
    if (connections[videoid]) {
      connections[videoid].forEach(client => {
        client.write(`data: ${message}\n\n`);
        messagesSent++;
      });
    }
  });
  
  subscribedTopics.add(topic);
}

app.post('/connect', async (req, res) => {
  const { userid, videoid } = req.body;

  if (!userid || !videoid) {
    return res.status(400).json({ error: 'userid and videoid are required' });
  }

  res.setHeader('Content-Type', 'text/event-stream');
  res.setHeader('Cache-Control', 'no-cache');
  res.setHeader('Connection', 'keep-alive');

  res.write(`data: ${JSON.stringify({ type: 'connected', userid, videoid, instance: INSTANCE_ID })}\n\n`);

  if (!connections[videoid]) {
    connections[videoid] = [];
  }
  
  connections[videoid].push(res);

  if (connections[videoid].length === 1) {
    await subscribeToVideo(videoid);
  }

  req.on('close', () => {
    if (connections[videoid]) {
      connections[videoid] = connections[videoid].filter(client => client !== res);
      
      if (connections[videoid].length === 0) {
        delete connections[videoid];
      }
    }
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
      }
    );
  } catch (err) {
    // Silently fail
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
