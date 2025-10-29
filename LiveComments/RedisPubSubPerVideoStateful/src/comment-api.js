const express = require('express');
const redis = require('redis');
const axios = require('axios');
const { StatisticsUtils } = require('../utils');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3000;
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';
const READER_API_MANAGER_URL = process.env.READER_API_MANAGER_URL || 'http://localhost:6000';

const comments = {};
let redisPublisher;

async function initRedis() {
  redisPublisher = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisPublisher.on('error', (err) => console.error('Redis Client Error', err));
  await redisPublisher.connect();
}

app.post('/comment', async (req, res) => {
  const { userid, videoid, comment } = req.body;

  if (!userid || !videoid || !comment) {
    return res.status(400).json({ error: 'userid, videoid, and comment are required' });
  }

  const commentData = {
    userid,
    videoid,
    comment,
    timestamp: new Date().toISOString()
  };

  if (!comments[videoid]) {
    comments[videoid] = [];
  }
  comments[videoid].push(commentData);

  try {
    const commentId = `comment:${videoid}:${Date.now()}:${userid}`;
    await redisPublisher.hSet(commentId, commentData);
  } catch (err) {
    console.error('Error storing comment in Redis:', err);
  }

  try {
    await redisPublisher.publish(`video:${videoid}`, JSON.stringify(commentData));
  } catch (err) {
    console.error('Error publishing to Redis:', err);
  }

  res.status(201).json({ success: true, comment: commentData });
});

async function sendStatistics() {
  try {
    const { channels, channelStats } = await StatisticsUtils.aggregateChannelStatistics(redisPublisher);
    const { commentsByVideo, totalComments } = StatisticsUtils.aggregateCommentStatistics(comments);

    await StatisticsUtils.sendStatistics(
      STATISTICS_API_URL,
      '/comment-api-statistics',
      {
        totalComments,
        commentsByVideo,
        activeTopics: channels,
        topicSubscribers: channelStats
      },
      '[Comment API]'
    );
  } catch (err) {
    console.error('Error sending statistics:', err.message);
  }
}

async function start() {
  await initRedis();
  app.listen(PORT, () => {
    console.log(`Comment API listening on port ${PORT}`);
  });

  setInterval(sendStatistics, 5000);
  setTimeout(sendStatistics, 3000);
}

start();
