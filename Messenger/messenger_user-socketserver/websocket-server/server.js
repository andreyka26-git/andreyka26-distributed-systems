const WebSocket = require('ws');
const redis = require('redis');
const { Pool } = require('pg');
const express = require('express');
const { v4: uuidv4 } = require('uuid');
const axios = require('axios');

class WebSocketServer {
    constructor() {
        this.serverId = uuidv4();
        this.port = process.env.PORT || 8080;
        this.redisClient = null;
        this.pgPool = null;
        this.wss = null;
        this.connectedUsers = new Map(); // userId -> websocket connection
        this.app = express();
        
        this.setupDatabase();
        this.setupRedis();
        this.setupWebSocket();
        this.setupRoutes();
        this.reportStatistics();
    }

    async setupDatabase() {
        this.pgPool = new Pool({
            user: process.env.DB_USER || 'postgres',
            host: process.env.DB_HOST || 'postgres',
            database: process.env.DB_NAME || 'messenger_db',
            password: process.env.DB_PASSWORD || 'postgres',
            port: process.env.DB_PORT || 5432,
        });

        try {
            await this.pgPool.query('SELECT NOW()');
            console.log(`[${this.serverId}] Connected to PostgreSQL`);
        } catch (error) {
            console.error(`[${this.serverId}] Error connecting to PostgreSQL:`, error);
        }
    }

    async setupRedis() {
        this.redisClient = redis.createClient({
            url: process.env.REDIS_URL || 'redis://redis:6379'
        });

        this.redisClient.on('error', (err) => {
            console.error(`[${this.serverId}] Redis error:`, err);
        });

        await this.redisClient.connect();
        console.log(`[${this.serverId}] Connected to Redis`);

        // Subscribe to message notifications
        const subscriber = this.redisClient.duplicate();
        await subscriber.connect();
        await subscriber.subscribe(`server:${this.serverId}`, this.handleServerMessage.bind(this));
    }

    setupWebSocket() {
        this.wss = new WebSocket.Server({ 
            port: this.port,
            path: '/ws'
        });

        this.wss.on('connection', async (ws, req) => {
            const userId = req.url.split('userId=')[1];
            
            if (!userId) {
                ws.close(4000, 'Missing userId parameter');
                return;
            }

            console.log(`[${this.serverId}] User ${userId} connected`);
            
            // Store connection and register in Redis
            this.connectedUsers.set(userId, ws);
            await this.redisClient.set(`user:${userId}`, this.serverId);
            
            // Setup message handling
            ws.on('message', async (data) => {
                try {
                    const message = JSON.parse(data.toString());
                    await this.handleClientMessage(userId, message);
                } catch (error) {
                    console.error(`[${this.serverId}] Error handling message:`, error);
                    ws.send(JSON.stringify({ error: 'Invalid message format' }));
                }
            });

            ws.on('close', async () => {
                console.log(`[${this.serverId}] User ${userId} disconnected`);
                this.connectedUsers.delete(userId);
                await this.redisClient.del(`user:${userId}`);
            });

            // Send connection confirmation
            ws.send(JSON.stringify({ 
                type: 'connected', 
                serverId: this.serverId,
                userId: userId 
            }));
        });

        console.log(`[${this.serverId}] WebSocket server listening on port ${this.port}`);
    }

    setupRoutes() {
        this.app.use(express.json());

        // Health check endpoint
        this.app.get('/health', (req, res) => {
            res.json({ 
                serverId: this.serverId,
                connectedUsers: this.connectedUsers.size,
                status: 'healthy'
            });
        });

        // Internal endpoint to receive messages from other servers
        this.app.post('/internal/message', async (req, res) => {
            const { chatId, message, fromServer } = req.body;
            await this.broadcastToLocalUsers(chatId, message);
            res.json({ success: true });
        });

        const httpPort = parseInt(this.port) + 1000;
        this.app.listen(httpPort, () => {
            console.log(`[${this.serverId}] HTTP server listening on port ${httpPort}`);
        });
    }

    async handleClientMessage(userId, message) {
        const { type, chatId, content } = message;

        if (type === 'send_message') {
            // Store message in database
            const result = await this.pgPool.query(
                'INSERT INTO messages (chat_id, sender_id, content) VALUES ($1, $2, $3) RETURNING *',
                [chatId, userId, content]
            );

            const savedMessage = result.rows[0];
            
            // Notify all participants
            await this.notifyParticipants(chatId, {
                type: 'new_message',
                message: savedMessage
            });
        }
    }

    async notifyParticipants(chatId, messageData) {
        try {
            // Get chat participants
            const chatResult = await this.pgPool.query(
                'SELECT participants FROM chats WHERE id = $1',
                [chatId]
            );

            if (chatResult.rows.length === 0) {
                console.error(`[${this.serverId}] Chat ${chatId} not found`);
                return;
            }

            const participants = chatResult.rows[0].participants;
            const websocketServers = new Set();

            // Find which servers handle each participant
            for (const participantId of participants) {
                const serverKey = await this.redisClient.get(`user:${participantId}`);
                if (serverKey) {
                    websocketServers.add(serverKey);
                }
            }

            // Fan out to all necessary servers
            for (const serverId of websocketServers) {
                if (serverId === this.serverId) {
                    // Local delivery
                    await this.broadcastToLocalUsers(chatId, messageData);
                } else {
                    // Remote delivery via Redis pub/sub
                    await this.redisClient.publish(`server:${serverId}`, JSON.stringify({
                        type: 'message_notification',
                        chatId,
                        messageData
                    }));
                }
            }
        } catch (error) {
            console.error(`[${this.serverId}] Error notifying participants:`, error);
        }
    }

    async handleServerMessage(message) {
        try {
            const data = JSON.parse(message);
            
            if (data.type === 'message_notification') {
                await this.broadcastToLocalUsers(data.chatId, data.messageData);
            }
        } catch (error) {
            console.error(`[${this.serverId}] Error handling server message:`, error);
        }
    }

    async broadcastToLocalUsers(chatId, messageData) {
        try {
            // Get chat participants
            const chatResult = await this.pgPool.query(
                'SELECT participants FROM chats WHERE id = $1',
                [chatId]
            );

            if (chatResult.rows.length === 0) return;

            const participants = chatResult.rows[0].participants;

            // Send to connected users on this server
            for (const participantId of participants) {
                const ws = this.connectedUsers.get(participantId.toString());
                if (ws && ws.readyState === WebSocket.OPEN) {
                    ws.send(JSON.stringify(messageData));
                }
            }
        } catch (error) {
            console.error(`[${this.serverId}] Error broadcasting to local users:`, error);
        }
    }

    async reportStatistics() {
        // Report statistics every 30 seconds
        setInterval(async () => {
            try {
                const stats = {
                    serverId: this.serverId,
                    type: 'websocket_server',
                    connectedUsers: Array.from(this.connectedUsers.keys()),
                    userCount: this.connectedUsers.size,
                    timestamp: new Date().toISOString()
                };

                await axios.post('http://statistics-api:3000/stats', stats);
            } catch (error) {
                console.error(`[${this.serverId}] Error reporting statistics:`, error);
            }
        }, 30000);
    }
}

// Start the server
const server = new WebSocketServer();

process.on('SIGTERM', async () => {
    console.log(`[${server.serverId}] Shutting down gracefully...`);
    if (server.redisClient) await server.redisClient.quit();
    if (server.pgPool) await server.pgPool.end();
    process.exit(0);
});