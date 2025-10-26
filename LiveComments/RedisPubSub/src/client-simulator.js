const axios = require('axios');

// Configuration
const NUM_VIDEOS = parseInt(process.env.NUM_VIDEOS || '200', 10);
const NUM_VIEWERS = parseInt(process.env.NUM_VIEWERS || '5000', 10);
const COMMENT_API_URL = process.env.COMMENT_API_URL || 'http://comment-api:3000';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://statistics-api:5000';
const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://reader-api-1:4000', 'http://reader-api-2:4000', 'http://reader-api-3:4000', 'http://reader-api-4:4000', 'http://reader-api-5:4000'];
const COMMENT_INTERVAL = parseInt(process.env.COMMENT_INTERVAL || '300000', 10); // 5 minutes default

// Client class to represent a single viewer
class Client {
  constructor(clientId, userId, videoId) {
    this.clientId = clientId;
    this.userId = userId;
    this.videoId = videoId;
    this.connectedReader = null;
    this.commentsGenerated = 0;
    this.commentsConsumed = 0;
    this.currentReaderIndex = Math.floor(Math.random() * READER_API_URLS.length);
    this.isConnected = false;
    this.stream = null;
  }

  // Connect to a Reader API for SSE
  async connect() {
    // Select random reader API on first connect
    const readerUrl = READER_API_URLS[this.currentReaderIndex % READER_API_URLS.length];
    this.connectedReader = readerUrl;

    // Only log occasionally to avoid spam
    const shouldLog = Math.random() < 0.02; // Log only 2% of connections
    if (shouldLog) {
      console.log(`[${this.clientId}] Connecting to ${readerUrl} for video ${this.videoId}`);
    }

    try {
      const response = await axios.post(`${readerUrl}/connect`, {
        userid: this.userId,
        videoid: this.videoId
      }, {
        responseType: 'stream',
        timeout: 30000, // Increased timeout to 30 seconds
        headers: {
          'Connection': 'keep-alive',
          'Accept': 'text/event-stream'
        },
        maxRedirects: 0
      });

      this.isConnected = true;
      if (shouldLog) {
        console.log(`[${this.clientId}] Connected to ${readerUrl}`);
      }

      // Keep reference to the stream
      this.stream = response.data;

      response.data.on('data', (chunk) => {
        const lines = chunk.toString().split('\n');
        lines.forEach(line => {
          if (line.startsWith('data: ')) {
            const data = line.substring(6);
            try {
              const message = JSON.parse(data);
              if (message.type === 'connected') {
                // Connection confirmed
              } else {
                this.commentsConsumed++;
              }
            } catch (e) {
              // Ignore parse errors
            }
          }
        });
      });

      response.data.on('end', () => {
        this.isConnected = false;
        // Silently reconnect without logging
        const backoff = 5000 + Math.random() * 5000;
        setTimeout(() => this.reconnect(), backoff);
      });

      response.data.on('error', (err) => {
        this.isConnected = false;
        // Silently reconnect without logging
        const backoff = 5000 + Math.random() * 5000;
        setTimeout(() => this.reconnect(), backoff);
      });

    } catch (err) {
      this.isConnected = false;
      // Don't log every error to avoid spam, just reconnect
      if (Math.random() < 0.01) { // Log only 1% of errors
        console.error(`[${this.clientId}] Connection error:`, err.message);
      }
      // Add exponential backoff for reconnection
      const backoff = 5000 + Math.random() * 5000; // 5-10 seconds
      setTimeout(() => this.reconnect(), backoff);
    }
  }

  async reconnect() {
    // Try next reader on reconnection
    this.currentReaderIndex++;
    await this.connect();
  }

  // Post a comment
  async postComment() {
    const comment = `Comment from ${this.clientId}`;
    
    try {
      await axios.post(`${COMMENT_API_URL}/comment`, {
        userid: this.userId,
        videoid: this.videoId,
        comment
      }, { timeout: 3000 });
      this.commentsGenerated++;
    } catch (err) {
      // Silently fail to avoid log spam
    }
  }

  getStats() {
    return {
      clientId: this.clientId,
      userId: this.userId,
      videoId: this.videoId,
      commentsGenerated: this.commentsGenerated,
      commentsConsumed: this.commentsConsumed,
      subscribedTopics: [`video:${this.videoId}`],
      connectedReader: this.connectedReader,
      isConnected: this.isConnected
    };
  }
}

// Generate video distribution (Zipf-like distribution)
function generateVideoDistribution() {
  const distribution = [];
  let remainingViewers = NUM_VIEWERS;
  
  // Calculate weights using simple decay
  const weights = [];
  for (let i = 1; i <= NUM_VIDEOS; i++) {
    weights.push(1 / i);
  }
  const totalWeight = weights.reduce((a, b) => a + b, 0);
  
  // Distribute viewers
  for (let i = 0; i < NUM_VIDEOS - 1; i++) {
    const proportion = weights[i] / totalWeight;
    const viewers = Math.max(1, Math.floor(NUM_VIEWERS * proportion));
    distribution.push({ videoId: `video${i + 1}`, viewers });
    remainingViewers -= viewers;
  }
  
  // Assign remaining viewers to last video
  distribution.push({ 
    videoId: `video${NUM_VIDEOS}`, 
    viewers: Math.max(1, remainingViewers)
  });
  
  return distribution;
}

// Main simulator
async function startSimulator() {
  console.log('='.repeat(60));
  console.log('LIVE COMMENTS SIMULATOR - Redis Pub/Sub Architecture Flaw Demo');
  console.log('='.repeat(60));
  console.log(`Configuration:`);
  console.log(`  - Videos: ${NUM_VIDEOS}`);
  console.log(`  - Viewers: ${NUM_VIEWERS}`);
  console.log(`  - Reader APIs: ${READER_API_URLS.length}`);
  console.log(`  - Comment Interval: ${COMMENT_INTERVAL}ms (${COMMENT_INTERVAL / 1000}s)`);
  console.log('='.repeat(60));

  const videoDistribution = generateVideoDistribution();
  console.log(`\nVideo Distribution (top 10):`);
  videoDistribution.slice(0, 10).forEach(v => {
    console.log(`  ${v.videoId}: ${v.viewers} viewers`);
  });
  console.log(`  ...`);
  console.log(`  Average: ${(NUM_VIEWERS / NUM_VIDEOS).toFixed(2)} viewers per video\n`);

  // Create all client instances
  const clients = [];
  let clientCounter = 1;

  for (const video of videoDistribution) {
    for (let i = 0; i < video.viewers; i++) {
      const client = new Client(
        `client-${clientCounter}`,
        `user${clientCounter}`,
        video.videoId
      );
      clients.push(client);
      clientCounter++;
    }
  }

  console.log(`Created ${clients.length} client instances\n`);

  // Connect all clients with staggered timing
  console.log('Connecting clients to Reader APIs...');
  console.log('This will take a few minutes to avoid overwhelming the system...\n');
  
  for (let i = 0; i < clients.length; i++) {
    const client = clients[i];
    // Stagger connections to avoid overwhelming the system
    // 200ms between each connection for stability
    setTimeout(() => {
      client.connect();
      
      // Log progress every 100 clients
      if ((i + 1) % 100 === 0) {
        console.log(`Progress: ${i + 1}/${clients.length} clients initiated connection...`);
      }
    }, i * 200); // 200ms between each connection
  }

  // Wait for initial connections to stabilize
  // With 200ms between connections, it takes ~100 seconds for 500 clients
  console.log('\nWaiting for initial connections to establish...');
  await new Promise(resolve => setTimeout(resolve, 60000));
  console.log('Initial connection wave complete. Clients will continue connecting in background.\n');

  // Start periodic comment posting for random clients
  setInterval(() => {
    // Pick a random subset of clients to post comments (simulate infrequent posting)
    const numCommenting = Math.floor(clients.length * 0.01); // 1% of clients post each interval
    const randomClients = [];
    
    for (let i = 0; i < numCommenting; i++) {
      const randomIndex = Math.floor(Math.random() * clients.length);
      randomClients.push(clients[randomIndex]);
    }

    randomClients.forEach(client => {
      if (client.isConnected) {
        client.postComment();
      }
    });
  }, COMMENT_INTERVAL);

  // Send aggregated statistics every 10 seconds
  setInterval(async () => {
    try {
      // Calculate per-reader distribution
      const readerDistribution = {};
      READER_API_URLS.forEach(url => {
        readerDistribution[url] = 0;
      });

      const videoStats = {};
      let totalConnected = 0;
      let totalCommentsGenerated = 0;
      let totalCommentsConsumed = 0;

      clients.forEach(client => {
        if (client.connectedReader) {
          readerDistribution[client.connectedReader]++;
        }
        if (client.isConnected) {
          totalConnected++;
        }
        
        totalCommentsGenerated += client.commentsGenerated;
        totalCommentsConsumed += client.commentsConsumed;

        if (!videoStats[client.videoId]) {
          videoStats[client.videoId] = { viewers: 0, connected: 0 };
        }
        videoStats[client.videoId].viewers++;
        if (client.isConnected) {
          videoStats[client.videoId].connected++;
        }
      });

      // Send to statistics API
      await axios.post(`${STATISTICS_API_URL}/client-statistics`, {
        clientId: 'simulator-aggregate',
        totalClients: clients.length,
        connectedClients: totalConnected,
        totalVideos: NUM_VIDEOS,
        uniqueVideosWatched: Object.keys(videoStats).length,
        readerDistribution,
        totalCommentsGenerated,
        totalCommentsConsumed,
        videoStats: Object.keys(videoStats).length
      });

      console.log(`📊 Statistics Update:`);
      console.log(`  Connected: ${totalConnected}/${clients.length} clients (${((totalConnected/clients.length)*100).toFixed(1)}%)`);
      console.log(`  Comments: ${totalCommentsGenerated} generated, ${totalCommentsConsumed} consumed`);
      console.log(`  Unique Videos Being Watched: ${Object.keys(videoStats).length}`);
      console.log(`  Reader Distribution:`);
      Object.entries(readerDistribution).forEach(([reader, count]) => {
        const readerName = reader.split('://')[1].split(':')[0];
        console.log(`    ${readerName}: ${count} clients`);
      });
      console.log('');

    } catch (err) {
      console.error('Error sending statistics:', err.message);
    }
  }, 10000);

  // Initial statistics after 3 seconds
  setTimeout(async () => {
    console.log('\n📊 Initial statistics sent\n');
  }, 3000);
}

// Start the simulator
startSimulator().catch(err => {
  console.error('Simulator error:', err);
  process.exit(1);
});
