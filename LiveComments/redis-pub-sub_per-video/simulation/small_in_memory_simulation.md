Showing that with 100 videos / 2.5k viewers, each reader api will be subscribed to 80% of all videos. To fix the problem we need to preserve {video => readerurl} mapping.

=== GENERATING VIEWER DISTRIBUTION ===
Total Videos: 100, Total Viewers: 2,500

Creating 1 top-tier video (200-300 viewers each)...
Creating 5 second-tier videos (50-100 viewers each)...
Creating 94 low-tier videos (1-20 viewers each, 1,823 viewers remaining)...

Total viewers assigned: 2,500
Total videos created: 100

=== VIEWER DISTRIBUTION STATISTICS ===
Total Videos: 100
Total Viewers: 2,500
Average Viewers per Video: 25.00
Max Viewers: 836
Min Viewers: 1

Top 20 Most Popular Videos:
 1. video_100            =>     836 viewers
 2. video_1              =>     275 viewers
 3. video_2              =>      95 viewers
 4. video_5              =>      93 viewers
 5. video_6              =>      77 viewers
 6. video_3              =>      71 viewers
 7. video_4              =>      66 viewers
 8. video_21             =>      20 viewers
 9. video_55             =>      20 viewers
10. video_10             =>      19 viewers
11. video_20             =>      19 viewers
12. video_39             =>      19 viewers
13. video_54             =>      19 viewers
14. video_78             =>      19 viewers
15. video_90             =>      19 viewers
16. video_8              =>      18 viewers
17. video_17             =>      18 viewers
18. video_23             =>      18 viewers
19. video_59             =>      18 viewers
20. video_71             =>      18 viewers

Viewer Count Distribution:
  1-100           viewers:     98 videos (98.00%)
  101-1000        viewers:      2 videos (2.00%)
  1001-5000       viewers:      0 videos (0.00%)
  5001-10000      viewers:      0 videos (0.00%)
  10001-50000     viewers:      0 videos (0.00%)
  50001+          viewers:      0 videos (0.00%)

=== SIMULATING PUB/SUB DISTRIBUTION ===
Reader API Instances: 5

Assigning viewers to random reader instances...
Processed 2,500 viewers

=== READER API SUBSCRIPTION RESULTS ===

reader_1:
  Total Subscribed Topics: 85 / 100 videos (85.00%)
  Total Viewers Served: 2,428
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     836 viewers
     2. video_1              =>     275 viewers
     3. video_2              =>      95 viewers
     4. video_5              =>      93 viewers
     5. video_6              =>      77 viewers
     6. video_3              =>      71 viewers
     7. video_4              =>      66 viewers
     8. video_21             =>      20 viewers
     9. video_55             =>      20 viewers
    10. video_10             =>      19 viewers

reader_2:
  Total Subscribed Topics: 84 / 100 videos (84.00%)
  Total Viewers Served: 2,432
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     836 viewers
     2. video_1              =>     275 viewers
     3. video_2              =>      95 viewers
     4. video_5              =>      93 viewers
     5. video_6              =>      77 viewers
     6. video_3              =>      71 viewers
     7. video_4              =>      66 viewers
     8. video_21             =>      20 viewers
     9. video_55             =>      20 viewers
    10. video_10             =>      19 viewers

reader_3:
  Total Subscribed Topics: 77 / 100 videos (77.00%)
  Total Viewers Served: 2,387
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     836 viewers
     2. video_1              =>     275 viewers
     3. video_2              =>      95 viewers
     4. video_5              =>      93 viewers
     5. video_6              =>      77 viewers
     6. video_3              =>      71 viewers
     7. video_4              =>      66 viewers
     8. video_21             =>      20 viewers
     9. video_55             =>      20 viewers
    10. video_10             =>      19 viewers

reader_4:
  Total Subscribed Topics: 84 / 100 videos (84.00%)
  Total Viewers Served: 2,420
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     836 viewers
     2. video_1              =>     275 viewers
     3. video_2              =>      95 viewers
     4. video_5              =>      93 viewers
     5. video_6              =>      77 viewers
     6. video_3              =>      71 viewers
     7. video_4              =>      66 viewers
     8. video_21             =>      20 viewers
     9. video_55             =>      20 viewers
    10. video_10             =>      19 viewers

reader_5:
  Total Subscribed Topics: 84 / 100 videos (84.00%)
  Total Viewers Served: 2,429
  Top 10 Most Popular Subscribed Topics:
     1. video_100            =>     836 viewers
     2. video_1              =>     275 viewers
     3. video_2              =>      95 viewers
     4. video_5              =>      93 viewers
     5. video_6              =>      77 viewers
     6. video_3              =>      71 viewers
     7. video_4              =>      66 viewers
     8. video_21             =>      20 viewers
     9. video_55             =>      20 viewers
    10. video_10             =>      19 viewers

=== OVERALL STATISTICS ===
Total Unique Subscriptions: 414
Average Subscriptions per Reader: 82.80

Subscription Distribution:
  Max Subscriptions: 85
  Min Subscriptions: 77
  Standard Deviation: 2.93
  Balance Factor: 9.66%

=== SIMULATION COMPLETE ===
Total execution time: 0.07 seconds