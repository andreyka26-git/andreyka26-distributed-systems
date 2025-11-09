`chat-to-user` implmenetation involves storing `User -> Chat` mapping.

The whole architecture has the following components:
- `chat-api` - RESTful, stateless service that handles chats and messages — e.g., retrieving and sending messages, getting chat details, etc. It notifies members of a chat about new messages by resolving their respective WebSocket servers and sending notifications to them.
- `websocket-server` - stateful service that handles clients' websocket connections. Clients connect there randomly and store the mapping in the redis (`user` -> `websocket-server`)
- `client-emulator` - simulates multiple clients
- `statistics-api` - optional thing for statistics and observability
- `storages` - we have `postgres` for chats and messages and `redis` for (user -> websocket-server) mapping.

The flow:
- client connects randomly to one websocket-server. If it looses connection - it reconnects to anyp other random one.
- websocket server upserts user -> websocket-server mapping to redis upon each new connection
- clients are posting messages via chat-api (stateless)
- when new message arrives - chat-api resolves all users that are in this chat, then it resolves all the websocket servers that handle these users, and it sends the notification there.