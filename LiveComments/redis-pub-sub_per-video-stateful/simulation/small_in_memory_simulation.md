# Small Scale Simulation Results - Stateful Architecture with ReaderApiManager

This simulation demonstrates the stateful video-to-reader mapping architecture where:
- ReaderApiManager assigns each video to a specific reader API instance
- Videos are distributed using round-robin allocation across reader instances
- All viewers of a video connect to the same assigned reader API instance

## Key Architecture Changes:
1. **ReaderApiManager**: Central service that manages video-to-reader mappings
2. **Stateful Assignment**: Each video is assigned to exactly one reader API instance
3. **Persistent Mapping**: Video assignments are stored in Redis and persist across sessions
4. **Client Resolution**: Clients query ReaderApiManager to find the correct reader API for their video

=== GENERATING VIEWER DISTRIBUTION ===
Total Videos: 100, Total Viewers: 2,500

Creating 1 top-tier video (200-300 viewers each)...
Creating 5 second-tier videos (50-100 viewers each)...
Creating 94 low-tier videos (1-20 viewers each, 1,898 viewers remaining)...

Total viewers assigned: 2,500
Total videos created: 100

=== VIEWER DISTRIBUTION STATISTICS ===
Total Videos: 100
Total Viewers: 2,500
Average Viewers per Video: 25.00
Max Viewers: 850
Min Viewers: 1

Top 20 Most Popular Videos:
 1. video_100            =>     850 viewers
 2. video_1              =>     262 viewers
 3. video_4              =>      80 viewers
 4. video_5              =>      72 viewers
 5. video_6              =>      71 viewers
 6. video_3              =>      63 viewers
 7. video_2              =>      54 viewers
 8. video_9              =>      20 viewers
 9. video_20             =>      20 viewers
10. video_26             =>      20 viewers
11. video_33             =>      20 viewers
12. video_42             =>      20 viewers
13. video_68             =>      20 viewers
14. video_87             =>      20 viewers
15. video_92             =>      20 viewers
16. video_98             =>      20 viewers
17. video_17             =>      19 viewers
18. video_89             =>      19 viewers
19. video_10             =>      18 viewers
20. video_31             =>      18 viewers

Viewer Count Distribution:
  1-100           viewers:     98 videos (98.00%)
  101-1000        viewers:      2 videos (2.00%)
  1001-5000       viewers:      0 videos (0.00%)
  5001-10000      viewers:      0 videos (0.00%)
  10001-50000     viewers:      0 videos (0.00%)
  50001+          viewers:      0 videos (0.00%)

=== SIMULATING STATEFUL VIDEO-TO-READER DISTRIBUTION ===
Reader API Instances: 5
ReaderApiManager assigns each video to a specific reader API

Assigning videos to reader instances using hash-based distribution...
Assigned 100 videos to 5 reader instances

=== READER API SUBSCRIPTION RESULTS ===

reader_1:
  Total Subscribed Topics: 20 / 100 videos (20.00%)
  Total Viewers Served: 275
  Top 10 Most Popular Subscribed Topics:
     1. video_2              =>      54 viewers
     2. video_20             =>      20 viewers
     3. video_98             =>      20 viewers
     4. video_89             =>      19 viewers
     5. video_84             =>      17 viewers
     6. video_93             =>      17 viewers
     7. video_70             =>      16 viewers
     8. video_16             =>      13 viewers
     9. video_52             =>      13 viewers
    10. video_25             =>      12 viewers

reader_2:
  Total Subscribed Topics: 20 / 100 videos (20.00%)
  Total Viewers Served: 280
  Top 10 Most Popular Subscribed Topics:
     1. video_3              =>      63 viewers
     2. video_26             =>      20 viewers
     3. video_17             =>      19 viewers
     4. video_49             =>      18 viewers
     5. video_30             =>      17 viewers
     6. video_21             =>      16 viewers
     7. video_62             =>      16 viewers
     8. video_80             =>      14 viewers
     9. video_67             =>      12 viewers
    10. video_58             =>      11 viewers

reader_3:
  Total Subscribed Topics: 20 / 100 videos (20.00%)
  Total Viewers Served: 312
  Top 10 Most Popular Subscribed Topics:
     1. video_4              =>      80 viewers
     2. video_9              =>      20 viewers
     3. video_68             =>      20 viewers
     4. video_31             =>      18 viewers
     5. video_13             =>      17 viewers
     6. video_45             =>      17 viewers
     7. video_59             =>      17 viewers
     8. video_72             =>      17 viewers
     9. video_22             =>      16 viewers
    10. video_27             =>      14 viewers

reader_4:
  Total Subscribed Topics: 19 / 100 videos (19.00%)
  Total Viewers Served: 233
  Top 10 Most Popular Subscribed Topics:
     1. video_5              =>      72 viewers
     2. video_87             =>      20 viewers
     3. video_73             =>      17 viewers
     4. video_78             =>      16 viewers
     5. video_46             =>      14 viewers
     6. video_64             =>      14 viewers
     7. video_14             =>      13 viewers
     8. video_96             =>      13 viewers
     9. video_91             =>      11 viewers
    10. video_32             =>       9 viewers

// !!!! celebrity problem (very popular video)
reader_5:
  Total Subscribed Topics: 21 / 100 videos (21.00%)
  Total Viewers Served: 1,400 
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     850 viewers
     2. video_1              =>     262 viewers
     3. video_6              =>      71 viewers
     4. video_33             =>      20 viewers
     5. video_42             =>      20 viewers
     6. video_92             =>      20 viewers
     7. video_10             =>      18 viewers
     8. video_83             =>      18 viewers
     9. video_79             =>      17 viewers
    10. video_47             =>      12 viewers

=== OVERALL STATISTICS ===
Total Unique Subscriptions: 100
Average Subscriptions per Reader: 20.00

Subscription Distribution:
  Max Subscriptions: 21
  Min Subscriptions: 19
  Standard Deviation: 0.63
  Balance Factor: 10.00%

=== SIMULATION COMPLETE ===
Total execution time: 0.07 seconds