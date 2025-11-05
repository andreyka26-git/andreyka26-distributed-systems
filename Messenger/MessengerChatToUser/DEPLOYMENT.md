# Messenger System - Deployment Guide

## Prerequisites

1. **Docker Desktop**: Ensure Docker Desktop is installed and running
2. **System Requirements**: Windows 10/11 with Docker Desktop or Linux with Docker and Docker Compose

## Quick Start

### Option 1: Docker Compose (Recommended)

1. **Start Docker Desktop**
   - Open Docker Desktop application
   - Wait for it to fully start (whale icon should be solid)

2. **Clone and Navigate**
   ```bash
   cd MessengerChatToUser
   ```

3. **Start the System**
   ```bash
   docker-compose up --build
   ```

4. **Verify Services**
   - WebSocket Servers: http://localhost:8081/health, http://localhost:8082/health, http://localhost:8083/health
   - Chat API: http://localhost:3001/health
   - Statistics API: http://localhost:3000/health
   - System Overview: http://localhost:3000/overview

### Option 2: Manual Testing (if Docker issues persist)

If Docker Desktop isn't available, you can test individual components:

1. **Install Node.js dependencies for each service:**
   ```bash
   cd websocket-server && npm install
   cd ../chat-api && npm install
   cd ../statistics-api && npm install
   cd ../client-simulator && npm install
   ```

2. **Start PostgreSQL and Redis locally** (or use cloud services)

3. **Update connection strings** in each service to point to your local databases

4. **Start services manually:**
   ```bash
   # Terminal 1
   cd websocket-server && npm start
   
   # Terminal 2  
   cd chat-api && npm start
   
   # Terminal 3
   cd statistics-api && npm start
   
   # Terminal 4
   cd client-simulator && npm start
   ```

## Troubleshooting

### Docker Issues

1. **"System cannot find the file specified"**
   - Ensure Docker Desktop is running
   - Try switching Docker context: `docker context use default`
   - Restart Docker Desktop

2. **"Version is obsolete" warning**
   - This is just a warning and can be ignored
   - The docker-compose.yml has been updated to remove the version field

3. **Port conflicts**
   - Stop other services using ports 3000, 3001, 5432, 6379, 8081-8083
   - Or modify the ports in docker-compose.yml

### Service Health Checks

Run the validation script:
```bash
# Windows
.\test-system.bat

# Linux/Mac
./test-system.sh
```

## System Architecture Validation

### Expected Behavior

1. **Client Connection**
   - 10 clients connect to random WebSocket servers
   - Redis tracks user-to-server mapping

2. **Chat Creation**
   - Chats are created via REST API
   - Stored in PostgreSQL

3. **Message Flow**
   - Client sends message via WebSocket
   - Server stores in PostgreSQL
   - Server finds participant servers via Redis
   - Message fans out to all relevant servers
   - Connected clients receive messages

4. **Statistics**
   - All services report metrics every 30 seconds
   - Available at http://localhost:3000/overview

### Testing the System

1. **Automated Test** (runs automatically with client-simulator)
   - Creates multiple chats
   - Sends messages between users
   - Validates fan-out behavior

2. **Manual Testing**
   ```bash
   # Create a chat
   curl -X POST http://localhost:3001/chats \
     -H "Content-Type: application/json" \
     -d '{"user_ids": [1, 2, 3]}'

   # Connect WebSocket (use any WebSocket client)
   ws://localhost:8081/ws?userId=1

   # Send message
   {"type": "send_message", "chatId": 1, "content": "Hello!"}

   # Check statistics
   curl http://localhost:3000/overview
   ```

## Performance Notes

- **Horizontal Scaling**: Add more WebSocket servers by duplicating the service in docker-compose.yml
- **Load Distribution**: Clients randomly connect to available servers
- **Redis Pub/Sub**: Handles inter-server communication efficiently
- **Database**: Uses connection pooling for PostgreSQL

## Production Considerations

1. **Security**: Add authentication, rate limiting, input validation
2. **Monitoring**: Integrate with observability tools
3. **Persistence**: Use persistent volumes for PostgreSQL
4. **Load Balancing**: Add nginx/HAProxy for WebSocket load balancing
5. **Scaling**: Use Redis Cluster and read replicas for PostgreSQL

## Expected Output

When running successfully, you should see:
```
[Client 1] Connected to server abc-123
[Client 2] Connected to server def-456  
[Client 1] Created chat 1 with participants: [1, 2, 3]
[Client 1] Sent message to chat 1: Hello from user 1!
[Client 2] New message in chat 1: Hello from user 1!
[WebSocket Server abc-123] Message fan-out to 2 servers
[Statistics API] Received stats from websocket_server:abc-123
```

The system demonstrates:
- ✅ Distributed WebSocket servers
- ✅ Redis-based user-to-server mapping  
- ✅ Message fan-out across servers
- ✅ PostgreSQL storage
- ✅ Real-time statistics
- ✅ Parallel client simulation