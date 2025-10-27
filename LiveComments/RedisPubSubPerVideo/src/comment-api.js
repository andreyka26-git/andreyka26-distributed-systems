const express = require('express');
const redis = require('redis');
const axios = require('axios');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3000;
const REDIS_HOST = process.env.REDIS_HOST || 'localhost';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';

const comments = {};
let redisPublisher;

async function initRedis() {
  redisPublisher = redis.createClient({ url: `redis://${REDIS_HOST}:6379` });
  redisPublisher.on('error', (err) => console.error('Redis Client Error', err));
  await redisPublisher.connect();
  console.log('Connected to Redis');
  
  await redisPublisher.flushDb();
  console.log('Redis database cleared');
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

  console.log(`New comment from user ${userid} on video ${videoid}: ${comment}`);

  try {
    const commentId = `comment:${videoid}:${Date.now()}:${userid}`;
    await redisPublisher.hSet(commentId, commentData);
    console.log(`Stored comment in Redis hashset: ${commentId}`);
  } catch (err) {
    console.error('Error storing comment in Redis:', err);
  }

  try {
    await redisPublisher.publish(`video:${videoid}`, JSON.stringify(commentData));
    console.log(`Published comment to video:${videoid} topic`);
  } catch (err) {
    console.error('Error publishing to Redis:', err);
  }

  res.status(201).json({ success: true, comment: commentData });
});

app.get('/health', (req, res) => {
  res.json({ status: 'ok', service: 'comment-api' });
});

async function sendStatistics() {
  try {
    const channels = await redisPublisher.sendCommand(['PUBSUB', 'CHANNELS', 'video:*']);
    
    const channelStats = {};
    for (const channel of channels) {
      const [, numSubs] = await redisPublisher.sendCommand(['PUBSUB', 'NUMSUB', channel]);
      channelStats[channel] = numSubs;
    }

    const commentsByVideo = {};
    let totalComments = 0;
    
    for (const [videoid, videoComments] of Object.entries(comments)) {
      const count = videoComments.length;
      commentsByVideo[videoid] = count;
      totalComments += count;
    }

    await axios.post(`${STATISTICS_API_URL}/comment-api-statistics`, {
      totalComments,
      commentsByVideo,
      activeTopics: channels,
      topicSubscribers: channelStats
    });

    console.log(`Sent Comment API statistics: ${totalComments} comments, ${channels.length} topics`);
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
