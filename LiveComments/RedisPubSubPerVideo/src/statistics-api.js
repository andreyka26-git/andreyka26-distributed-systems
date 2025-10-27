const express = require('express');
const redis = require('redis');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 5000;
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';

const commentApiStats = {};
const readerApiStats = {};
const clientStats = {};

let redisClient;

async function initRedis() {
  redisClient = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisClient.on('error', (err) => console.error('Redis Client Error', err));
  await redisClient.connect();
  console.log('Connected to Redis');
}

app.post('/comment-api-statistics', (req, res) => {
  const { totalComments, commentsByVideo, activeTopics, topicSubscribers } = req.body;

  commentApiStats.data = {
    totalComments: totalComments || 0,
    commentsByVideo: commentsByVideo || {},
    activeTopics: activeTopics || [],
    topicSubscribers: topicSubscribers || {},
    lastUpdated: new Date().toISOString()
  };

  console.log('Updated Comment API statistics');
  res.status(200).json({ success: true });
});

app.post('/reader-api-statistics', (req, res) => {
  const { instanceId, activeConnections, subscribedTopics, messagesSent } = req.body;

  if (!instanceId) {
    return res.status(400).json({ error: 'instanceId is required' });
  }

  readerApiStats[instanceId] = {
    instanceId,
    activeConnections: activeConnections || 0,
    subscribedTopics: subscribedTopics || [],
    messagesSent: messagesSent || 0,
    lastUpdated: new Date().toISOString()
  };

  console.log(`Updated statistics for ${instanceId}`);
  res.status(200).json({ success: true });
});

app.post('/client-statistics', (req, res) => {
  const { clientId, userId, videoId, commentsGenerated, commentsConsumed, subscribedTopics, connectedReader } = req.body;

  if (!clientId) {
    return res.status(400).json({ error: 'clientId is required' });
  }

  clientStats[clientId] = {
    clientId,
    userId,
    videoId,
    commentsGenerated: commentsGenerated || 0,
    commentsConsumed: commentsConsumed || 0,
    subscribedTopics: subscribedTopics || [],
    connectedReader: connectedReader || null,
    lastUpdated: new Date().toISOString()
  };

  console.log(`Updated statistics for ${clientId}`);
  res.status(200).json({ success: true });
});

app.get('/statistics', async (req, res) => {
  const readers = Object.values(readerApiStats);
  const clients = Object.values(clientStats);

  const allComments = [];
  try {
    const keys = await redisClient.keys('comment:*');
    
    for (const key of keys) {
      const commentData = await redisClient.hGetAll(key);
      if (commentData && Object.keys(commentData).length > 0) {
        allComments.push({ id: key, ...commentData });
      }
    }
    
    console.log(`Retrieved ${allComments.length} comments from Redis`);
  } catch (err) {
    console.error('Error retrieving comments from Redis:', err);
  }

  res.json({
    commentApi: commentApiStats.data || {},
    readerApis: {
      totalReaders: readers.length,
      totalActiveConnections: readers.reduce((sum, r) => sum + r.activeConnections, 0),
      totalMessagesSent: readers.reduce((sum, r) => sum + r.messagesSent, 0),
      allSubscribedTopics: [...new Set(readers.flatMap(r => r.subscribedTopics))],
      readers
    },
    clients: {
      totalClients: clients.length,
      totalCommentsGenerated: clients.reduce((sum, c) => sum + c.commentsGenerated, 0),
      totalCommentsConsumed: clients.reduce((sum, c) => sum + c.commentsConsumed, 0),
      clients
    },
    storedComments: {
      total: allComments.length,
      comments: allComments
    },
    timestamp: new Date().toISOString()
  });
});

app.get('/health', (req, res) => {
  res.json({ status: 'ok', service: 'statistics-api' });
});

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`Statistics API listening on port ${PORT}`);
  });
}

start();
