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
Creating 94 low-tier videos (1-20 viewers each, 1,884 viewers remaining)...

Total viewers assigned: 2,500
Total videos created: 100

=== VIEWER DISTRIBUTION STATISTICS ===
Total Videos: 100
Total Viewers: 2,500
Average Viewers per Video: 25.00
Max Viewers: 880
Min Viewers: 1

Top 20 Most Popular Videos:
 1. video_100            =>     880 viewers
 2. video_1              =>     266 viewers
 3. video_2              =>      92 viewers
 4. video_4              =>      86 viewers
 5. video_5              =>      60 viewers
 6. video_3              =>      57 viewers
 7. video_6              =>      55 viewers
 8. video_7              =>      20 viewers
 9. video_10             =>      20 viewers
10. video_17             =>      20 viewers
11. video_80             =>      20 viewers
12. video_91             =>      20 viewers
13. video_8              =>      19 viewers
14. video_38             =>      19 viewers
15. video_50             =>      19 viewers
16. video_67             =>      19 viewers
17. video_68             =>      19 viewers
18. video_92             =>      19 viewers
19. video_24             =>      18 viewers
20. video_70             =>      18 viewers

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
  Total Viewers Served: 310
  Top 10 Most Popular Subscribed Topics:
     1. video_2              =>      92 viewers
     2. video_7              =>      20 viewers
     3. video_70             =>      18 viewers
     4. video_48             =>      17 viewers
     5. video_57             =>      17 viewers
     6. video_61             =>      16 viewers
     7. video_66             =>      16 viewers
     8. video_89             =>      16 viewers
     9. video_84             =>      15 viewers
    10. video_39             =>      14 viewers

reader_2:
  Total Subscribed Topics: 20 / 100 videos (20.00%)
  Total Viewers Served: 308
  Top 10 Most Popular Subscribed Topics:
     1. video_3              =>      57 viewers
     2. video_17             =>      20 viewers
     3. video_80             =>      20 viewers
     4. video_8              =>      19 viewers
     5. video_67             =>      19 viewers
     6. video_26             =>      17 viewers
     7. video_76             =>      17 viewers
     8. video_85             =>      16 viewers
     9. video_99             =>      15 viewers
    10. video_35             =>      14 viewers

reader_3:
  Total Subscribed Topics: 20 / 100 videos (20.00%)
  Total Viewers Served: 270
  Top 10 Most Popular Subscribed Topics:
     1. video_4              =>      86 viewers
     2. video_68             =>      19 viewers
     3. video_90             =>      18 viewers
     4. video_81             =>      17 viewers
     5. video_22             =>      16 viewers
     6. video_95             =>      16 viewers
     7. video_27             =>      14 viewers
     8. video_40             =>      14 viewers
     9. video_59             =>      10 viewers
    10. video_13             =>       9 viewers

reader_4:
  Total Subscribed Topics: 19 / 100 videos (19.00%)
  Total Viewers Served: 222
  Top 10 Most Popular Subscribed Topics:
     1. video_5              =>      60 viewers
     2. video_91             =>      20 viewers
     3. video_50             =>      19 viewers
     4. video_19             =>      17 viewers
     5. video_41             =>      17 viewers
     6. video_82             =>      14 viewers
     7. video_87             =>      14 viewers
     8. video_96             =>      11 viewers
     9. video_78             =>       9 viewers
    10. video_55             =>       7 viewers

reader_5:
  Total Subscribed Topics: 21 / 100 videos (21.00%)
  Total Viewers Served: 1,390
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     880 viewers
     2. video_1              =>     266 viewers
     3. video_6              =>      55 viewers
     4. video_10             =>      20 viewers
     5. video_38             =>      19 viewers
     6. video_92             =>      19 viewers
     7. video_24             =>      18 viewers
     8. video_33             =>      17 viewers
     9. video_60             =>      12 viewers
    10. video_65             =>      12 viewers

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