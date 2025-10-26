const axios = require('axios');
const EventSource = require('eventsource');

const CLIENT_ID = process.env.CLIENT_ID || 'client-1';
const USER_ID = process.env.USER_ID || 'user1';
const VIDEO_ID = process.env.VIDEO_ID || 'video1';
const COMMENT_API_URL = process.env.COMMENT_API_URL || 'http://localhost:3000';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';
const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://localhost:4001', 'http://localhost:4002', 'http://localhost:4003'];

let eventSource = null;
let currentReaderIndex = 0;
let connectedReader = null;

// Client statistics
let commentsGenerated = 0;
let commentsConsumed = 0;

// Send statistics to Statistics API
async function sendStatistics() {
  try {
    await axios.post(`${STATISTICS_API_URL}/client-statistics`, {
      clientId: CLIENT_ID,
      userId: USER_ID,
      videoId: VIDEO_ID,
      commentsGenerated,
      commentsConsumed,
      subscribedTopics: [`video:${VIDEO_ID}`],
      connectedReader
    });
    console.log(`[${CLIENT_ID}] 📊 Sent statistics: generated=${commentsGenerated}, consumed=${commentsConsumed}`);
  } catch (err) {
    console.error(`[${CLIENT_ID}] Error sending statistics:`, err.message);
  }
}

// Connect to a random Reader API for SSE
async function connectToReader() {
  // Select random reader API (or round-robin on reconnect)
  const readerUrl = READER_API_URLS[currentReaderIndex % READER_API_URLS.length];
  currentReaderIndex++;
  connectedReader = readerUrl;

  console.log(`[${CLIENT_ID}] Connecting to ${readerUrl} for video ${VIDEO_ID}`);

  try {
    // First, establish the SSE connection by calling POST /connect
    const response = await axios.post(`${readerUrl}/connect`, {
      userid: USER_ID,
      videoid: VIDEO_ID
    }, {
      responseType: 'stream'
    });

    console.log(`[${CLIENT_ID}] Connected to ${readerUrl}`);

    response.data.on('data', (chunk) => {
      const lines = chunk.toString().split('\n');
      lines.forEach(line => {
        if (line.startsWith('data: ')) {
          const data = line.substring(6);
          try {
            const message = JSON.parse(data);
            if (message.type === 'connected') {
              console.log(`[${CLIENT_ID}] Connection confirmed: ${JSON.stringify(message)}`);
            } else {
              commentsConsumed++;
              console.log(`[${CLIENT_ID}] 📩 New comment received:`, message);
            }
          } catch (e) {
            console.log(`[${CLIENT_ID}] Received:`, data);
          }
        }
      });
    });

    response.data.on('end', () => {
      console.log(`[${CLIENT_ID}] Connection closed. Reconnecting...`);
      connectedReader = null;
      setTimeout(connectToReader, 2000);
    });

    response.data.on('error', (err) => {
      console.error(`[${CLIENT_ID}] Stream error:`, err.message);
      connectedReader = null;
      setTimeout(connectToReader, 2000);
    });

  } catch (err) {
    console.error(`[${CLIENT_ID}] Connection error:`, err.message);
    console.log(`[${CLIENT_ID}] Retrying in 2 seconds...`);
    connectedReader = null;
    setTimeout(connectToReader, 2000);
  }
}

// Post a comment periodically
async function postComment() {
  const comment = `Hello from ${CLIENT_ID} at ${new Date().toISOString()}`;
  
  try {
    const response = await axios.post(`${COMMENT_API_URL}/comment`, {
      userid: USER_ID,
      videoid: VIDEO_ID,
      comment
    });
    commentsGenerated++;
    console.log(`[${CLIENT_ID}] 📝 Posted comment:`, comment);
  } catch (err) {
    console.error(`[${CLIENT_ID}] Error posting comment:`, err.message);
  }
}

async function start() {
  console.log(`[${CLIENT_ID}] Starting client for user ${USER_ID} watching video ${VIDEO_ID}`);
  
  // Connect to reader API
  await connectToReader();

  // Post comments every 10 seconds
  setInterval(postComment, 10000);

  // Post first comment after 5 seconds
  setTimeout(postComment, 5000);

  // Send statistics every 5 seconds
  setInterval(sendStatistics, 5000);

  // Send initial statistics after 2 seconds
  setTimeout(sendStatistics, 2000);
}

start();
