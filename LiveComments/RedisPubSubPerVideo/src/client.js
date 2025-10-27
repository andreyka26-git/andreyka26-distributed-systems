const axios = require('axios');

const COMMENT_API_URL = process.env.COMMENT_API_URL || 'http://localhost:3000';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';
const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://localhost:4001', 'http://localhost:4002', 'http://localhost:4003'];

// Client configuration map
const CLIENT_CONFIGS = [
  { clientId: 'client-1', userId: 'user1', videoId: 'video1' },
  { clientId: 'client-2', userId: 'user2', videoId: 'video1' },
  { clientId: 'client-3', userId: 'user3', videoId: 'video1' },
  { clientId: 'client-4', userId: 'user4', videoId: 'video2' },
  { clientId: 'client-5', userId: 'user5', videoId: 'video2' },
  { clientId: 'client-6', userId: 'user6', videoId: 'video2' },
  { clientId: 'client-7', userId: 'user7', videoId: 'video3' },
  { clientId: 'client-8', userId: 'user8', videoId: 'video3' },
  { clientId: 'client-9', userId: 'user9', videoId: 'video3' },
  { clientId: 'client-10', userId: 'user10', videoId: 'video3' },
];

class Client {
  constructor(clientId, userId, videoId) {
    this.clientId = clientId;
    this.userId = userId;
    this.videoId = videoId;
    this.connectedReader = null;
    this.commentsGenerated = 0;
    this.commentsConsumed = 0;
  }

  async sendStatistics() {
    try {
      await axios.post(`${STATISTICS_API_URL}/client-statistics`, {
        clientId: this.clientId,
        userId: this.userId,
        videoId: this.videoId,
        commentsGenerated: this.commentsGenerated,
        commentsConsumed: this.commentsConsumed,
        subscribedTopics: [`video:${this.videoId}`],
        connectedReader: this.connectedReader
      });
      console.log(`[${this.clientId}] Sent statistics: generated=${this.commentsGenerated}, consumed=${this.commentsConsumed}`);
    } catch (err) {
      console.error(`[${this.clientId}] Error sending statistics:`, err.message);
    }
  }

  async connectToReader() {
    const readerUrl = READER_API_URLS[Math.floor(Math.random() * READER_API_URLS.length)];
    this.connectedReader = readerUrl;

    console.log(`[${this.clientId}] Connecting to ${readerUrl} for video ${this.videoId}`);

    try {
      const response = await axios.post(`${readerUrl}/connect`, {
        userid: this.userId,
        videoid: this.videoId
      }, {
        responseType: 'stream'
      });

      console.log(`[${this.clientId}] Connected to ${readerUrl}`);

      const handleMessage = (data) => {
        try {
          const message = JSON.parse(data);
          if (message.type === 'connected') {
            console.log(`[${this.clientId}] Connection confirmed: ${JSON.stringify(message)}`);
          } else {
            this.commentsConsumed++;
            console.log(`[${this.clientId}] New comment received:`, message);
          }
        } catch (e) {
          console.log(`[${this.clientId}] Received:`, data);
        }
      };

      const reconnect = () => {
        this.connectedReader = null;
        setTimeout(() => this.connectToReader(), 2000);
      };

      response.data.on('data', (chunk) => {
        chunk.toString().split('\n').forEach(line => {
          if (line.startsWith('data: ')) {
            handleMessage(line.substring(6));
          }
        });
      });

      response.data.on('end', () => {
        console.log(`[${this.clientId}] Connection closed. Reconnecting...`);
        reconnect();
      });

      response.data.on('error', (err) => {
        console.error(`[${this.clientId}] Stream error:`, err.message);
        reconnect();
      });

    } catch (err) {
      console.error(`[${this.clientId}] Connection error:`, err.message);
      console.log(`[${this.clientId}] Retrying in 2 seconds...`);
      this.connectedReader = null;
      setTimeout(() => this.connectToReader(), 2000);
    }
  }

  async postComment() {
    const comment = `Hello from ${this.clientId} at ${new Date().toISOString()}`;
    
    try {
      const response = await axios.post(`${COMMENT_API_URL}/comment`, {
        userid: this.userId,
        videoid: this.videoId,
        comment
      });
      this.commentsGenerated++;
      console.log(`[${this.clientId}] Posted comment:`, comment);
    } catch (err) {
      console.error(`[${this.clientId}] Error posting comment:`, err.message);
    }
  }

  async start() {
    console.log(`[${this.clientId}] Starting client for user ${this.userId} watching video ${this.videoId}`);
    
    await this.connectToReader();

    setInterval(() => this.postComment(), 10000);
    setInterval(() => this.sendStatistics(), 5000);
    
    setTimeout(() => this.sendStatistics(), 2000);
  }
}

// Start all clients
async function startAllClients() {
  console.log(`Starting ${CLIENT_CONFIGS.length} clients...`);
  
  const clients = CLIENT_CONFIGS.map(config => 
    new Client(config.clientId, config.userId, config.videoId)
  );

  // Start all clients
  clients.forEach(client => client.start());
}

startAllClients();
