const express = require('express');
const { Pool } = require('pg');
const redis = require('redis');
const cors = require('cors');
const axios = require('axios');

class ChatAPI {
    constructor() {
        this.app = express();
        this.port = process.env.PORT || 3001;
        this.pgPool = null;
        this.redisClient = null;
        
        this.setupDatabase();
        this.setupRedis();
        this.setupMiddleware();
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
            console.log('[Chat API] Connected to PostgreSQL');
        } catch (error) {
            console.error('[Chat API] Error connecting to PostgreSQL:', error);
        }
    }

    async setupRedis() {
        this.redisClient = redis.createClient({
            url: process.env.REDIS_URL || 'redis://redis:6379'
        });

        this.redisClient.on('error', (err) => {
            console.error('[Chat API] Redis error:', err);
        });

        await this.redisClient.connect();
        console.log('[Chat API] Connected to Redis');
    }

    setupMiddleware() {
        this.app.use(cors());
        this.app.use(express.json());
        
        // Request logging
        this.app.use((req, res, next) => {
            console.log(`[Chat API] ${req.method} ${req.path}`, req.body);
            next();
        });
    }

    setupRoutes() {
        // Health check
        this.app.get('/health', (req, res) => {
            res.json({ status: 'healthy', service: 'chat-api' });
        });

        // Create a new chat
        this.app.post('/chats', async (req, res) => {
            try {
                const { user_ids } = req.body;

                if (!user_ids || !Array.isArray(user_ids) || user_ids.length < 2) {
                    return res.status(400).json({ 
                        error: 'user_ids must be an array with at least 2 users' 
                    });
                }

                const result = await this.pgPool.query(
                    'INSERT INTO chats (participants) VALUES ($1) RETURNING *',
                    [user_ids]
                );

                const chat = result.rows[0];
                
                res.status(201).json({
                    success: true,
                    chat: {
                        id: chat.id,
                        participants: chat.participants,
                        created_at: chat.created_at
                    }
                });

                console.log(`[Chat API] Created chat ${chat.id} with participants:`, user_ids);
            } catch (error) {
                console.error('[Chat API] Error creating chat:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get chat details
        this.app.get('/chats/:chatId', async (req, res) => {
            try {
                const { chatId } = req.params;

                const chatResult = await this.pgPool.query(
                    'SELECT * FROM chats WHERE id = $1',
                    [chatId]
                );

                if (chatResult.rows.length === 0) {
                    return res.status(404).json({ error: 'Chat not found' });
                }

                const messagesResult = await this.pgPool.query(
                    'SELECT * FROM messages WHERE chat_id = $1 ORDER BY created_at DESC LIMIT 50',
                    [chatId]
                );

                res.json({
                    chat: chatResult.rows[0],
                    messages: messagesResult.rows
                });
            } catch (error) {
                console.error('[Chat API] Error getting chat:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get all chats for a user
        this.app.get('/users/:userId/chats', async (req, res) => {
            try {
                const { userId } = req.params;

                const result = await this.pgPool.query(
                    'SELECT * FROM chats WHERE $1 = ANY(participants) ORDER BY created_at DESC',
                    [parseInt(userId)]
                );

                res.json({ chats: result.rows });
            } catch (error) {
                console.error('[Chat API] Error getting user chats:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get messages for a chat
        this.app.get('/chats/:chatId/messages', async (req, res) => {
            try {
                const { chatId } = req.params;
                const { limit = 50, offset = 0 } = req.query;

                const result = await this.pgPool.query(
                    'SELECT * FROM messages WHERE chat_id = $1 ORDER BY created_at DESC LIMIT $2 OFFSET $3',
                    [chatId, limit, offset]
                );

                res.json({ messages: result.rows });
            } catch (error) {
                console.error('[Chat API] Error getting messages:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Send a message (alternative to WebSocket)
        this.app.post('/chats/:chatId/messages', async (req, res) => {
            try {
                const { chatId } = req.params;
                const { sender_id, content } = req.body;

                if (!sender_id || !content) {
                    return res.status(400).json({ 
                        error: 'sender_id and content are required' 
                    });
                }

                // Store message in database
                const result = await this.pgPool.query(
                    'INSERT INTO messages (chat_id, sender_id, content) VALUES ($1, $2, $3) RETURNING *',
                    [chatId, sender_id, content]
                );

                const message = result.rows[0];

                // Notify participants via WebSocket servers
                await this.notifyParticipants(chatId, {
                    type: 'new_message',
                    message: message
                });

                res.status(201).json({ 
                    success: true, 
                    message: message 
                });
            } catch (error) {
                console.error('[Chat API] Error sending message:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get system statistics
        this.app.get('/system/stats', async (req, res) => {
            try {
                const chatCount = await this.pgPool.query('SELECT COUNT(*) FROM chats');
                const messageCount = await this.pgPool.query('SELECT COUNT(*) FROM messages');
                
                // Get Redis keys count
                const userKeys = await this.redisClient.keys('user:*');
                
                res.json({
                    chats: parseInt(chatCount.rows[0].count),
                    messages: parseInt(messageCount.rows[0].count),
                    connectedUsers: userKeys.length,
                    redisKeys: userKeys.length
                });
            } catch (error) {
                console.error('[Chat API] Error getting stats:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });
    }

    async notifyParticipants(chatId, messageData) {
        try {
            // Get chat participants
            const chatResult = await this.pgPool.query(
                'SELECT participants FROM chats WHERE id = $1',
                [chatId]
            );

            if (chatResult.rows.length === 0) {
                console.error(`[Chat API] Chat ${chatId} not found`);
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

            // Notify all WebSocket servers
            for (const serverId of websocketServers) {
                await this.redisClient.publish(`server:${serverId}`, JSON.stringify({
                    type: 'message_notification',
                    chatId,
                    messageData
                }));
            }
        } catch (error) {
            console.error('[Chat API] Error notifying participants:', error);
        }
    }

    async reportStatistics() {
        setInterval(async () => {
            try {
                const chatCount = await this.pgPool.query('SELECT COUNT(*) FROM chats');
                const messageCount = await this.pgPool.query('SELECT COUNT(*) FROM messages');
                
                const stats = {
                    serverId: 'chat-api',
                    type: 'chat_api',
                    chatCount: parseInt(chatCount.rows[0].count),
                    messageCount: parseInt(messageCount.rows[0].count),
                    timestamp: new Date().toISOString()
                };

                await axios.post('http://statistics-api:3000/stats', stats);
            } catch (error) {
                console.error('[Chat API] Error reporting statistics:', error);
            }
        }, 30000);
    }

    start() {
        this.app.listen(this.port, () => {
            console.log(`[Chat API] Server listening on port ${this.port}`);
        });
    }
}

const chatAPI = new ChatAPI();
chatAPI.start();

process.on('SIGTERM', async () => {
    console.log('[Chat API] Shutting down gracefully...');
    if (chatAPI.redisClient) await chatAPI.redisClient.quit();
    if (chatAPI.pgPool) await chatAPI.pgPool.end();
    process.exit(0);
});