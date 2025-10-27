const axios = require('axios');

const CLIENT_ID = process.env.CLIENT_ID || 'client-1';
const USER_ID = process.env.USER_ID || 'user1';
const VIDEO_ID = process.env.VIDEO_ID || 'video1';
const COMMENT_API_URL = process.env.COMMENT_API_URL || 'http://localhost:3000';
const STATISTICS_API_URL = process.env.STATISTICS_API_URL || 'http://localhost:5000';
const READER_API_URLS = process.env.READER_API_URLS 
  ? process.env.READER_API_URLS.split(',') 
  : ['http://localhost:4001', 'http://localhost:4002', 'http://localhost:4003'];

let connectedReader = null;
let commentsGenerated = 0;
let commentsConsumed = 0;

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
    console.log(`[${CLIENT_ID}] Sent statistics: generated=${commentsGenerated}, consumed=${commentsConsumed}`);
  } catch (err) {
    console.error(`[${CLIENT_ID}] Error sending statistics:`, err.message);
  }
}

async function connectToReader() {
  const readerUrl = READER_API_URLS[Math.floor(Math.random() * READER_API_URLS.length)];
  connectedReader = readerUrl;

  console.log(`[${CLIENT_ID}] Connecting to ${readerUrl} for video ${VIDEO_ID}`);

  try {
    const response = await axios.post(`${readerUrl}/connect`, {
      userid: USER_ID,
      videoid: VIDEO_ID
    }, {
      responseType: 'stream'
    });

    console.log(`[${CLIENT_ID}] Connected to ${readerUrl}`);

    const handleMessage = (data) => {
      try {
        const message = JSON.parse(data);
        if (message.type === 'connected') {
          console.log(`[${CLIENT_ID}] Connection confirmed: ${JSON.stringify(message)}`);
        } else {
          commentsConsumed++;
          console.log(`[${CLIENT_ID}] New comment received:`, message);
        }
      } catch (e) {
        console.log(`[${CLIENT_ID}] Received:`, data);
      }
    };

    const reconnect = () => {
      connectedReader = null;
      setTimeout(connectToReader, 2000);
    };

    response.data.on('data', (chunk) => {
      chunk.toString().split('\n').forEach(line => {
        if (line.startsWith('data: ')) {
          handleMessage(line.substring(6));
        }
      });
    });

    response.data.on('end', () => {
      console.log(`[${CLIENT_ID}] Connection closed. Reconnecting...`);
      reconnect();
    });

    response.data.on('error', (err) => {
      console.error(`[${CLIENT_ID}] Stream error:`, err.message);
      reconnect();
    });

  } catch (err) {
    console.error(`[${CLIENT_ID}] Connection error:`, err.message);
    console.log(`[${CLIENT_ID}] Retrying in 2 seconds...`);
    connectedReader = null;
    setTimeout(connectToReader, 2000);
  }
}

async function postComment() {
  const comment = `Hello from ${CLIENT_ID} at ${new Date().toISOString()}`;
  
  try {
    const response = await axios.post(`${COMMENT_API_URL}/comment`, {
      userid: USER_ID,
      videoid: VIDEO_ID,
      comment
    });
    commentsGenerated++;
    console.log(`[${CLIENT_ID}] Posted comment:`, comment);
  } catch (err) {
    console.error(`[${CLIENT_ID}] Error posting comment:`, err.message);
  }
}

async function start() {
  console.log(`[${CLIENT_ID}] Starting client for user ${USER_ID} watching video ${VIDEO_ID}`);
  
  await connectToReader();

  setInterval(postComment, 10000);
  setInterval(sendStatistics, 5000);
  
  setTimeout(sendStatistics, 2000);
}

start();
