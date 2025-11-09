const express = require('express');
const cors = require('cors');

class StatisticsAPI {
    constructor() {
        this.app = express();
        this.port = process.env.PORT || 3000;
        this.statistics = new Map(); // Store statistics in memory
        
        this.setupMiddleware();
        this.setupRoutes();
        this.setupCleanup();
    }

    setupMiddleware() {
        this.app.use(cors());
        this.app.use(express.json());
        
        // Request logging
        this.app.use((req, res, next) => {
            console.log(`[Statistics API] ${req.method} ${req.path}`);
            next();
        });
    }

    setupRoutes() {
        // Health check
        this.app.get('/health', (req, res) => {
            res.json({ status: 'healthy', service: 'statistics-api' });
        });

        // Receive statistics from other services
        this.app.post('/stats', (req, res) => {
            try {
                const { serverId, type, timestamp, ...data } = req.body;
                
                if (!serverId || !type) {
                    return res.status(400).json({ 
                        error: 'serverId and type are required' 
                    });
                }

                // Store statistics
                this.statistics.set(`${type}:${serverId}`, {
                    serverId,
                    type,
                    timestamp: timestamp || new Date().toISOString(),
                    data
                });

                console.log(`[Statistics API] Received stats from ${type}:${serverId}`);
                res.json({ success: true });
            } catch (error) {
                console.error('[Statistics API] Error storing statistics:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get all statistics
        this.app.get('/stats', (req, res) => {
            try {
                const allStats = {};
                
                for (const [key, value] of this.statistics.entries()) {
                    const [type, serverId] = key.split(':');
                    
                    if (!allStats[type]) {
                        allStats[type] = [];
                    }
                    
                    allStats[type].push(value);
                }

                res.json(allStats);
            } catch (error) {
                console.error('[Statistics API] Error getting statistics:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get statistics by type
        this.app.get('/stats/:type', (req, res) => {
            try {
                const { type } = req.params;
                const typeStats = [];
                
                for (const [key, value] of this.statistics.entries()) {
                    if (key.startsWith(`${type}:`)) {
                        typeStats.push(value);
                    }
                }

                res.json({ type, statistics: typeStats });
            } catch (error) {
                console.error('[Statistics API] Error getting type statistics:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Get aggregated system overview
        this.app.get('/overview', (req, res) => {
            try {
                const overview = {
                    websocketServers: [],
                    totalConnectedUsers: 0,
                    totalChats: 0,
                    totalMessages: 0,
                    uniqueUsers: new Set(),
                    lastUpdated: new Date().toISOString()
                };

                // Process WebSocket server statistics
                for (const [key, value] of this.statistics.entries()) {
                    if (key.startsWith('websocket_server:')) {
                        overview.websocketServers.push({
                            serverId: value.serverId,
                            connectedUsers: value.data.connectedUsers || [],
                            userCount: value.data.userCount || 0,
                            lastSeen: value.timestamp
                        });
                        
                        overview.totalConnectedUsers += value.data.userCount || 0;
                        
                        if (value.data.connectedUsers) {
                            value.data.connectedUsers.forEach(userId => 
                                overview.uniqueUsers.add(userId)
                            );
                        }
                    }
                    
                    if (key.startsWith('chat_api:')) {
                        overview.totalChats = value.data.chatCount || 0;
                        overview.totalMessages = value.data.messageCount || 0;
                    }
                }

                overview.uniqueUserCount = overview.uniqueUsers.size;
                delete overview.uniqueUsers; // Remove Set object from response

                res.json(overview);
            } catch (error) {
                console.error('[Statistics API] Error getting overview:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });

        // Clear all statistics
        this.app.delete('/stats', (req, res) => {
            try {
                this.statistics.clear();
                console.log('[Statistics API] Cleared all statistics');
                res.json({ success: true, message: 'All statistics cleared' });
            } catch (error) {
                console.error('[Statistics API] Error clearing statistics:', error);
                res.status(500).json({ error: 'Internal server error' });
            }
        });
    }

    setupCleanup() {
        // Clean up old statistics every 5 minutes
        setInterval(() => {
            const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
            
            for (const [key, value] of this.statistics.entries()) {
                const statTime = new Date(value.timestamp);
                if (statTime < fiveMinutesAgo) {
                    this.statistics.delete(key);
                    console.log(`[Statistics API] Cleaned up old stat: ${key}`);
                }
            }
        }, 5 * 60 * 1000); // 5 minutes
    }

    start() {
        this.app.listen(this.port, () => {
            console.log(`[Statistics API] Server listening on port ${this.port}`);
        });
    }
}

const statisticsAPI = new StatisticsAPI();
statisticsAPI.start();

process.on('SIGTERM', () => {
    console.log('[Statistics API] Shutting down gracefully...');
    process.exit(0);
});