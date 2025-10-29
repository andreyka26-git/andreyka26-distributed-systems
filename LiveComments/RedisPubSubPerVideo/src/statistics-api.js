const express = require('express');
const redis = require('redis');
const { StatisticsUtils } = require('./utils');

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

  res.status(200).json({ success: true });
});

app.get('/statistics', async (req, res) => {
  const allComments = await StatisticsUtils.retrieveCommentsFromRedis(redisClient);
  
  const aggregatedStats = StatisticsUtils.aggregateSystemStatistics({
    commentApiStats,
    readerApiStats,
    clientStats,
    allComments
  });

  res.json(aggregatedStats);
});

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`Statistics API listening on port ${PORT}`);
  });
}

start();
