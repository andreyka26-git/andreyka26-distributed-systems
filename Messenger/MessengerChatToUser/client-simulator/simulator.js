const WebSocket = require('ws');
const axios = require('axios');

class ClientSimulator {
    constructor(userId, websocketServers, chatApiUrl, statisticsApiUrl) {
        this.userId = userId;
        this.websocketServers = websocketServers;
        this.chatApiUrl = chatApiUrl;
        this.statisticsApiUrl = statisticsApiUrl;
        this.ws = null;
        this.connected = false;
        this.receivedMessages = [];
        this.chats = [];
    }

    async connect() {
        return new Promise((resolve, reject) => {
            try {
                // Choose a random WebSocket server
                const serverUrl = this.websocketServers[
                    Math.floor(Math.random() * this.websocketServers.length)
                ];
                
                console.log(`[Client ${this.userId}] Connecting to ${serverUrl}`);
                
                this.ws = new WebSocket(`${serverUrl}/ws?userId=${this.userId}`);
                
                this.ws.on('open', () => {
                    console.log(`[Client ${this.userId}] Connected to WebSocket`);
                    this.connected = true;
                    resolve();
                });

                this.ws.on('message', (data) => {
                    try {
                        const message = JSON.parse(data.toString());
                        this.handleMessage(message);
                    } catch (error) {
                        console.error(`[Client ${this.userId}] Error parsing message:`, error);
                    }
                });

                this.ws.on('close', () => {
                    console.log(`[Client ${this.userId}] WebSocket connection closed`);
                    this.connected = false;
                });

                this.ws.on('error', (error) => {
                    console.error(`[Client ${this.userId}] WebSocket error:`, error);
                    reject(error);
                });

                // Timeout after 10 seconds
                setTimeout(() => {
                    if (!this.connected) {
                        reject(new Error('Connection timeout'));
                    }
                }, 10000);
            } catch (error) {
                reject(error);
            }
        });
    }

    handleMessage(message) {
        console.log(`[Client ${this.userId}] Received:`, message);
        
        switch (message.type) {
            case 'connected':
                console.log(`[Client ${this.userId}] Connected to server ${message.serverId}`);
                break;
            case 'new_message':
                this.receivedMessages.push(message.message);
                console.log(`[Client ${this.userId}] New message in chat ${message.message.chat_id}: ${message.message.content}`);
                break;
            default:
                console.log(`[Client ${this.userId}] Unknown message type: ${message.type}`);
        }
    }

    async createChat(participantIds) {
        try {
            const response = await axios.post(`${this.chatApiUrl}/chats`, {
                user_ids: [this.userId, ...participantIds]
            });
            
            const chat = response.data.chat;
            this.chats.push(chat);
            console.log(`[Client ${this.userId}] Created chat ${chat.id} with participants: ${chat.participants}`);
            return chat;
        } catch (error) {
            console.error(`[Client ${this.userId}] Error creating chat:`, error.response?.data || error.message);
            return null;
        }
    }

    async sendMessage(chatId, content) {
        if (!this.connected || !this.ws) {
            console.error(`[Client ${this.userId}] Not connected to WebSocket`);
            return false;
        }

        try {
            const message = {
                type: 'send_message',
                chatId: chatId,
                content: content
            };

            this.ws.send(JSON.stringify(message));
            console.log(`[Client ${this.userId}] Sent message to chat ${chatId}: ${content}`);
            return true;
        } catch (error) {
            console.error(`[Client ${this.userId}] Error sending message:`, error);
            return false;
        }
    }

    async getMyChats() {
        try {
            const response = await axios.get(`${this.chatApiUrl}/users/${this.userId}/chats`);
            this.chats = response.data.chats;
            console.log(`[Client ${this.userId}] Retrieved ${this.chats.length} chats`);
            return this.chats;
        } catch (error) {
            console.error(`[Client ${this.userId}] Error getting chats:`, error.response?.data || error.message);
            return [];
        }
    }

    disconnect() {
        if (this.ws) {
            this.ws.close();
            this.connected = false;
            console.log(`[Client ${this.userId}] Disconnected`);
        }
    }

    getStats() {
        return {
            userId: this.userId,
            connected: this.connected,
            chatsCount: this.chats.length,
            receivedMessagesCount: this.receivedMessages.length,
            chats: this.chats.map(chat => ({
                id: chat.id,
                participants: chat.participants
            }))
        };
    }
}

class SimulationRunner {
    constructor() {
        this.clients = [];
        this.websocketServers = [
            'ws://websocket-server-1:8080',
            'ws://websocket-server-2:8080',
            'ws://websocket-server-3:8080'
        ];
        this.chatApiUrl = 'http://chat-api:3001';
        this.statisticsApiUrl = 'http://statistics-api:3000';
        this.running = false;
    }

    async waitForServices() {
        console.log('[Simulator] Waiting for services to be ready...');
        
        const maxRetries = 30;
        let retries = 0;

        while (retries < maxRetries) {
            try {
                // Check Chat API
                await axios.get(`${this.chatApiUrl}/health`);
                
                // Check Statistics API
                await axios.get(`${this.statisticsApiUrl}/health`);
                
                console.log('[Simulator] All services are ready!');
                return true;
            } catch (error) {
                retries++;
                console.log(`[Simulator] Services not ready, retry ${retries}/${maxRetries}`);
                await new Promise(resolve => setTimeout(resolve, 5000));
            }
        }

        throw new Error('Services did not become ready in time');
    }

    async createClients(count) {
        console.log(`[Simulator] Creating ${count} clients...`);
        
        for (let i = 1; i <= count; i++) {
            const client = new ClientSimulator(
                i,
                this.websocketServers,
                this.chatApiUrl,
                this.statisticsApiUrl
            );
            this.clients.push(client);
        }

        // Connect all clients in parallel
        await Promise.all(this.clients.map(client => client.connect()));
        console.log(`[Simulator] All ${count} clients connected!`);
    }

    async runSimulation() {
        console.log('[Simulator] Starting simulation...');
        this.running = true;

        try {
            // Wait for services
            await this.waitForServices();
            
            // Create clients
            await this.createClients(10);

            // Wait a bit for connections to stabilize
            await new Promise(resolve => setTimeout(resolve, 2000));

            // Create some chats
            console.log('[Simulator] Creating chats...');
            
            // Chat 1: Users 1, 2, 3
            const chat1 = await this.clients[0].createChat([2, 3]);
            
            // Chat 2: Users 4, 5, 6, 7
            const chat2 = await this.clients[3].createChat([5, 6, 7]);
            
            // Chat 3: Users 1, 8, 9, 10
            const chat3 = await this.clients[0].createChat([8, 9, 10]);

            await new Promise(resolve => setTimeout(resolve, 1000));

            // Send messages
            console.log('[Simulator] Sending messages...');
            
            if (chat1) {
                await this.clients[0].sendMessage(chat1.id, 'Hello from user 1!');
                await new Promise(resolve => setTimeout(resolve, 500));
                await this.clients[1].sendMessage(chat1.id, 'Hi back from user 2!');
                await new Promise(resolve => setTimeout(resolve, 500));
                await this.clients[2].sendMessage(chat1.id, 'User 3 here!');
            }

            if (chat2) {
                await this.clients[3].sendMessage(chat2.id, 'Group chat started by user 4');
                await new Promise(resolve => setTimeout(resolve, 500));
                await this.clients[4].sendMessage(chat2.id, 'User 5 joining the conversation');
            }

            if (chat3) {
                await this.clients[0].sendMessage(chat3.id, 'Another chat from user 1');
                await new Promise(resolve => setTimeout(resolve, 500));
                await this.clients[7].sendMessage(chat3.id, 'User 8 responding');
            }

            // Keep running for a while
            console.log('[Simulator] Simulation running... (will run for 2 minutes)');
            
            // Send periodic messages
            let messageCounter = 0;
            const messageInterval = setInterval(async () => {
                if (!this.running) {
                    clearInterval(messageInterval);
                    return;
                }

                messageCounter++;
                const randomClient = this.clients[Math.floor(Math.random() * this.clients.length)];
                
                if (randomClient.chats.length > 0) {
                    const randomChat = randomClient.chats[Math.floor(Math.random() * randomClient.chats.length)];
                    await randomClient.sendMessage(randomChat.id, `Periodic message #${messageCounter} from user ${randomClient.userId}`);
                }
            }, 10000); // Every 10 seconds

            // Report statistics periodically
            const statsInterval = setInterval(async () => {
                if (!this.running) {
                    clearInterval(statsInterval);
                    return;
                }

                await this.reportStatistics();
            }, 15000); // Every 15 seconds

            // Run for 2 minutes
            await new Promise(resolve => setTimeout(resolve, 120000));

            // Stop intervals
            clearInterval(messageInterval);
            clearInterval(statsInterval);

            console.log('[Simulator] Simulation completed successfully!');
            
        } catch (error) {
            console.error('[Simulator] Simulation failed:', error);
        } finally {
            this.running = false;
            await this.cleanup();
        }
    }

    async reportStatistics() {
        try {
            const connectedClients = this.clients.filter(c => c.connected);
            
            const stats = {
                serverId: 'client-simulator',
                type: 'client_simulator',
                totalClients: this.clients.length,
                connectedClients: connectedClients.length,
                clientStats: connectedClients.map(c => c.getStats()),
                timestamp: new Date().toISOString()
            };

            await axios.post(`${this.statisticsApiUrl}/stats`, stats);
            console.log(`[Simulator] Reported statistics: ${connectedClients.length}/${this.clients.length} clients connected`);
        } catch (error) {
            console.error('[Simulator] Error reporting statistics:', error);
        }
    }

    async cleanup() {
        console.log('[Simulator] Cleaning up...');
        
        // Disconnect all clients
        await Promise.all(this.clients.map(client => client.disconnect()));
        
        console.log('[Simulator] Cleanup complete');
    }
}

// Run the simulation
async function main() {
    const simulator = new SimulationRunner();
    
    process.on('SIGTERM', async () => {
        console.log('[Simulator] Received SIGTERM, shutting down...');
        simulator.running = false;
        await simulator.cleanup();
        process.exit(0);
    });

    process.on('SIGINT', async () => {
        console.log('[Simulator] Received SIGINT, shutting down...');
        simulator.running = false;
        await simulator.cleanup();
        process.exit(0);
    });

    await simulator.runSimulation();
}

main().catch(error => {
    console.error('[Simulator] Fatal error:', error);
    process.exit(1);
});