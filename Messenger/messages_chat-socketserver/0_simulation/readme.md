# Goal

The goal is to show that `chat -> websocket_server` mapping works much better on large chats comparing to `user -> websocket_server` mapping.

## 1000-large-chats_50-shards_100-websockets_output

12476 messages

Redis shard reads: min: 144 max: 419 median: 242.50 average: 249.52
Websocket updates: min: 27 max: 245 median: 120.50 average: 124.76
Users per websocket server: min: 16952 max: 154667 median: 73586.50 average: 74946.60

## 1000-large-chats_500-shards_1000-websockets_output

12476 messages

Redis shard reads: min: 5 max: 124 median: 24.00 average: 28.55
Websocket updates: min: 5 max: 79 median: 17.00 average: 19.80
Users per websocket server: min: 0 max: 38151 median: 6776.00 average: 7494.66