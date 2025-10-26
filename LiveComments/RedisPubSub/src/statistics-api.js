const express = require('express');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 5000;

// In-memory storage for all statistics
const commentApiStats = {};
const readerApiStats = {};
const clientStats = {};

// Comment API statistics
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

// Reader API statistics
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

// Client statistics
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

// Get all statistics
app.get('/statistics', (req, res) => {
  // Process reader API stats
  const readers = Object.values(readerApiStats);
  const totalReaders = readers.length;
  const totalActiveConnections = readers.reduce((sum, reader) => sum + reader.activeConnections, 0);
  const totalMessagesSent = readers.reduce((sum, reader) => sum + reader.messagesSent, 0);
  const allReaderTopics = [...new Set(readers.flatMap(reader => reader.subscribedTopics))];

  // Process client stats
  const clients = Object.values(clientStats);
  const totalClients = clients.length;
  const totalCommentsGenerated = clients.reduce((sum, client) => sum + client.commentsGenerated, 0);
  const totalCommentsConsumed = clients.reduce((sum, client) => sum + client.commentsConsumed, 0);

  res.json({
    commentApi: commentApiStats.data || {},
    readerApis: {
      totalReaders,
      totalActiveConnections,
      totalMessagesSent,
      allSubscribedTopics: allReaderTopics,
      readers
    },
    clients: {
      totalClients,
      totalCommentsGenerated,
      totalCommentsConsumed,
      clients
    },
    timestamp: new Date().toISOString()
  });
});

app.get('/health', (req, res) => {
  res.json({ status: 'ok', service: 'statistics-api' });
});

app.listen(PORT, () => {
  console.log(`Statistics API listening on port ${PORT}`);
});
