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
Creating 94 low-tier videos (1-20 viewers each, 1,891 viewers remaining)...

Total viewers assigned: 2,500
Total videos created: 100

=== VIEWER DISTRIBUTION STATISTICS ===
Total Videos: 100
Total Viewers: 2,500
Average Viewers per Video: 25.00
Max Viewers: 868
Min Viewers: 1

All Videos (sorted by video ID):
  1. video_1         =>  266 viewers
  2. video_2         =>   75 viewers
  3. video_3         =>   79 viewers
  4. video_4         =>   62 viewers
  5. video_5         =>   58 viewers
  6. video_6         =>   69 viewers
  7. video_7         =>   13 viewers
  8. video_8         =>   10 viewers
  9. video_9         =>    9 viewers
 10. video_10        =>   13 viewers
 11. video_11        =>    6 viewers
 12. video_12        =>   14 viewers
 13. video_13        =>   17 viewers
 14. video_14        =>    9 viewers
 15. video_15        =>    7 viewers
 16. video_16        =>   17 viewers
 17. video_17        =>    8 viewers
 18. video_18        =>    3 viewers
 19. video_19        =>   16 viewers
 20. video_20        =>   11 viewers
 21. video_21        =>   16 viewers
 22. video_22        =>    7 viewers
 23. video_23        =>    8 viewers
 24. video_24        =>   20 viewers
 25. video_25        =>    9 viewers
 26. video_26        =>    3 viewers
 27. video_27        =>    7 viewers
 28. video_28        =>    9 viewers
 29. video_29        =>    7 viewers
 30. video_30        =>    3 viewers
 31. video_31        =>    9 viewers
 32. video_32        =>    6 viewers
 33. video_33        =>    3 viewers
 34. video_34        =>   19 viewers
 35. video_35        =>   16 viewers
 36. video_36        =>    4 viewers
 37. video_37        =>    1 viewers
 38. video_38        =>   16 viewers
 39. video_39        =>    2 viewers
 40. video_40        =>    4 viewers
 41. video_41        =>   19 viewers
 42. video_42        =>   10 viewers
 43. video_43        =>    4 viewers
 44. video_44        =>   12 viewers
 45. video_45        =>    7 viewers
 46. video_46        =>   15 viewers
 47. video_47        =>   19 viewers
 48. video_48        =>   12 viewers
 49. video_49        =>    3 viewers
 50. video_50        =>    5 viewers
 51. video_51        =>    7 viewers
 52. video_52        =>   16 viewers
 53. video_53        =>    7 viewers
 54. video_54        =>   17 viewers
 55. video_55        =>    8 viewers
 56. video_56        =>   18 viewers
 57. video_57        =>    7 viewers
 58. video_58        =>   16 viewers
 59. video_59        =>    8 viewers
 60. video_60        =>   20 viewers
 61. video_61        =>   15 viewers
 62. video_62        =>   19 viewers
 63. video_63        =>   14 viewers
 64. video_64        =>    5 viewers
 65. video_65        =>    4 viewers
 66. video_66        =>   19 viewers
 67. video_67        =>    3 viewers
 68. video_68        =>    7 viewers
 69. video_69        =>    3 viewers
 70. video_70        =>    3 viewers
 71. video_71        =>    1 viewers
 72. video_72        =>   14 viewers
 73. video_73        =>   18 viewers
 74. video_74        =>   18 viewers
 75. video_75        =>   19 viewers
 76. video_76        =>    5 viewers
 77. video_77        =>    9 viewers
 78. video_78        =>   15 viewers
 79. video_79        =>    7 viewers
 80. video_80        =>    8 viewers
 81. video_81        =>   12 viewers
 82. video_82        =>   18 viewers
 83. video_83        =>   13 viewers
 84. video_84        =>   15 viewers
 85. video_85        =>   13 viewers
 86. video_86        =>   17 viewers
 87. video_87        =>    3 viewers
 88. video_88        =>   12 viewers
 89. video_89        =>   18 viewers
 90. video_90        =>   18 viewers
 91. video_91        =>    6 viewers
 92. video_92        =>   15 viewers
 93. video_93        =>   16 viewers
 94. video_94        =>   14 viewers
 95. video_95        =>   11 viewers
 96. video_96        =>   19 viewers
 97. video_97        =>   11 viewers
 98. video_98        =>   15 viewers
 99. video_99        =>   19 viewers
100. video_100       =>  868 viewers

Viewer Count Distribution:
  0          viewers:   0 videos (0.00%)
  1-10       viewers:  45 videos (45.00%)
  11-20      viewers:  48 videos (48.00%)
  21-50      viewers:   0 videos (0.00%)
  51-100     viewers:   5 videos (5.00%)
  101-200    viewers:   0 videos (0.00%)
  200+       viewers:   2 videos (2.00%)

=== SIMULATING STATEFUL VIDEO-TO-READER DISTRIBUTION ===
Reader API Instances: 5
ReaderApiManager assigns each video to a specific reader API

Assigning videos to reader instances using round-robin...
Assigned 100 videos to 5 reader instances

=== READER API SUBSCRIPTION RESULTS ===

reader_1:
  Total Subscribed Topics: 20
  Total Viewers Served: 543

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  266 viewers
      2. video_6         =>   69 viewers
      3. video_11        =>    6 viewers
      4. video_16        =>   17 viewers
      5. video_21        =>   16 viewers
      6. video_26        =>    3 viewers
      7. video_31        =>    9 viewers
      8. video_36        =>    4 viewers
      9. video_41        =>   19 viewers
     10. video_46        =>   15 viewers
     11. video_51        =>    7 viewers
     12. video_56        =>   18 viewers
     13. video_61        =>   15 viewers
     14. video_66        =>   19 viewers
     15. video_71        =>    1 viewers
     16. video_76        =>    5 viewers
     17. video_81        =>   12 viewers
     18. video_86        =>   17 viewers
     19. video_91        =>    6 viewers
     20. video_96        =>   19 viewers

reader_2:
  Total Subscribed Topics: 20
  Total Viewers Served: 275

  All Subscribed Topics (sorted by video ID):
      1. video_2         =>   75 viewers
      2. video_7         =>   13 viewers
      3. video_12        =>   14 viewers
      4. video_17        =>    8 viewers
      5. video_22        =>    7 viewers
      6. video_27        =>    7 viewers
      7. video_32        =>    6 viewers
      8. video_37        =>    1 viewers
      9. video_42        =>   10 viewers
     10. video_47        =>   19 viewers
     11. video_52        =>   16 viewers
     12. video_57        =>    7 viewers
     13. video_62        =>   19 viewers
     14. video_67        =>    3 viewers
     15. video_72        =>   14 viewers
     16. video_77        =>    9 viewers
     17. video_82        =>   18 viewers
     18. video_87        =>    3 viewers
     19. video_92        =>   15 viewers
     20. video_97        =>   11 viewers

reader_3:
  Total Subscribed Topics: 20
  Total Viewers Served: 294

  All Subscribed Topics (sorted by video ID):
      1. video_3         =>   79 viewers
      2. video_8         =>   10 viewers
      3. video_13        =>   17 viewers
      4. video_18        =>    3 viewers
      5. video_23        =>    8 viewers
      6. video_28        =>    9 viewers
      7. video_33        =>    3 viewers
      8. video_38        =>   16 viewers
      9. video_43        =>    4 viewers
     10. video_48        =>   12 viewers
     11. video_53        =>    7 viewers
     12. video_58        =>   16 viewers
     13. video_63        =>   14 viewers
     14. video_68        =>    7 viewers
     15. video_73        =>   18 viewers
     16. video_78        =>   15 viewers
     17. video_83        =>   13 viewers
     18. video_88        =>   12 viewers
     19. video_93        =>   16 viewers
     20. video_98        =>   15 viewers

reader_4:
  Total Subscribed Topics: 20
  Total Viewers Served: 283

  All Subscribed Topics (sorted by video ID):
      1. video_4         =>   62 viewers
      2. video_9         =>    9 viewers
      3. video_14        =>    9 viewers
      4. video_19        =>   16 viewers
      5. video_24        =>   20 viewers
      6. video_29        =>    7 viewers
      7. video_34        =>   19 viewers
      8. video_39        =>    2 viewers
      9. video_44        =>   12 viewers
     10. video_49        =>    3 viewers
     11. video_54        =>   17 viewers
     12. video_59        =>    8 viewers
     13. video_64        =>    5 viewers
     14. video_69        =>    3 viewers
     15. video_74        =>   18 viewers
     16. video_79        =>    7 viewers
     17. video_84        =>   15 viewers
     18. video_89        =>   18 viewers
     19. video_94        =>   14 viewers
     20. video_99        =>   19 viewers

reader_5:
  Total Subscribed Topics: 20
  Total Viewers Served: 1,105

  All Subscribed Topics (sorted by video ID):
      1. video_5         =>   58 viewers
      2. video_10        =>   13 viewers
      3. video_15        =>    7 viewers
      4. video_20        =>   11 viewers
      5. video_25        =>    9 viewers
      6. video_30        =>    3 viewers
      7. video_35        =>   16 viewers
      8. video_40        =>    4 viewers
      9. video_45        =>    7 viewers
     10. video_50        =>    5 viewers
     11. video_55        =>    8 viewers
     12. video_60        =>   20 viewers
     13. video_65        =>    4 viewers
     14. video_70        =>    3 viewers
     15. video_75        =>   19 viewers
     16. video_80        =>    8 viewers
     17. video_85        =>   13 viewers
     18. video_90        =>   18 viewers
     19. video_95        =>   11 viewers
     20. video_100       =>  868 viewers

=== OVERALL STATISTICS ===
Total Unique Subscriptions: 100
Average Subscriptions per Reader: 20.00

Subscription Distribution:
  Max Subscriptions: 20
  Min Subscriptions: 20
  Standard Deviation: 0.00
  Balance Factor: 0.00%

=== SIMULATION COMPLETE ===
Total execution time: 0.06 seconds

## Key Observations from Stateful Architecture:

### Perfect Video Distribution
- Each reader API instance handles exactly 20 videos (100 videos ÷ 5 readers)
- Round-robin assignment ensures perfect balance in terms of video count
- Standard deviation of 0.00% shows perfectly balanced video distribution

### Viewer Load Imbalance
- While video distribution is perfect, viewer load varies significantly:
  - reader_5: 1,105 viewers (due to video_100 with 868 viewers)
  - reader_1: 543 viewers
  - reader_3: 294 viewers
  - reader_4: 283 viewers  
  - reader_2: 275 viewers

### Benefits of Stateful Architecture
1. **Predictable Assignment**: Each video always maps to the same reader API
2. **Simplified Client Logic**: Clients query once to find their reader API
3. **Perfect Video Balance**: Round-robin ensures equal video distribution
4. **Persistent Mappings**: Assignments survive restarts and are cached in Redis

### Trade-offs
1. **Viewer Load Imbalance**: Popular videos can create hotspots on specific readers
2. **Single Point of Failure**: ReaderApiManager becomes critical component
3. **Less Dynamic**: Cannot redistribute load based on real-time viewer counts

This stateful approach is ideal when video assignments need to be consistent and predictable, at the cost of some load balancing flexibility.
Total Videos: 100, Total Viewers: 2,500

Creating 1 top-tier video (200-300 viewers each)...
Creating 5 second-tier videos (50-100 viewers each)...
Creating 94 low-tier videos (1-20 viewers each, 1,927 viewers remaining)...

Total viewers assigned: 2,500
Total videos created: 100

=== VIEWER DISTRIBUTION STATISTICS ===
Total Videos: 100
Total Viewers: 2,500
Average Viewers per Video: 25.00
Max Viewers: 908
Min Viewers: 1

All Videos (sorted by video ID):
  1. video_1         =>  210 viewers
  2. video_2         =>   87 viewers
  3. video_3         =>   75 viewers
  4. video_4         =>   71 viewers
  5. video_5         =>   62 viewers
  6. video_6         =>   68 viewers
  7. video_7         =>    9 viewers
  8. video_8         =>    1 viewers
  9. video_9         =>   19 viewers
 10. video_10        =>   19 viewers
 11. video_11        =>   10 viewers
 12. video_12        =>   10 viewers
 13. video_13        =>   13 viewers
 14. video_14        =>    1 viewers
 15. video_15        =>    4 viewers
 16. video_16        =>   10 viewers
 17. video_17        =>    2 viewers
 18. video_18        =>    2 viewers
 19. video_19        =>   12 viewers
 20. video_20        =>   10 viewers
 21. video_21        =>   10 viewers
 22. video_22        =>   13 viewers
 23. video_23        =>   18 viewers
 24. video_24        =>   11 viewers
 25. video_25        =>    7 viewers
 26. video_26        =>   17 viewers
 27. video_27        =>   13 viewers
 28. video_28        =>   14 viewers
 29. video_29        =>   20 viewers
 30. video_30        =>   14 viewers
 31. video_31        =>    3 viewers
 32. video_32        =>   18 viewers
 33. video_33        =>   11 viewers
 34. video_34        =>    2 viewers
 35. video_35        =>    7 viewers
 36. video_36        =>   12 viewers
 37. video_37        =>   19 viewers
 38. video_38        =>   19 viewers
 39. video_39        =>    1 viewers
 40. video_40        =>    8 viewers
 41. video_41        =>    1 viewers
 42. video_42        =>   18 viewers
 43. video_43        =>   15 viewers
 44. video_44        =>   16 viewers
 45. video_45        =>   19 viewers
 46. video_46        =>   15 viewers
 47. video_47        =>    6 viewers
 48. video_48        =>   19 viewers
 49. video_49        =>   10 viewers
 50. video_50        =>    9 viewers
 51. video_51        =>   18 viewers
 52. video_52        =>   14 viewers
 53. video_53        =>   20 viewers
 54. video_54        =>   17 viewers
 55. video_55        =>    6 viewers
 56. video_56        =>    5 viewers
 57. video_57        =>    4 viewers
 58. video_58        =>    1 viewers
 59. video_59        =>   19 viewers
 60. video_60        =>   15 viewers
 61. video_61        =>   14 viewers
 62. video_62        =>   15 viewers
 63. video_63        =>    9 viewers
 64. video_64        =>    3 viewers
 65. video_65        =>    6 viewers
 66. video_66        =>   16 viewers
 67. video_67        =>    6 viewers
 68. video_68        =>    3 viewers
 69. video_69        =>   19 viewers
 70. video_70        =>   20 viewers
 71. video_71        =>    9 viewers
 72. video_72        =>    4 viewers
 73. video_73        =>   14 viewers
 74. video_74        =>   18 viewers
 75. video_75        =>   11 viewers
 76. video_76        =>    8 viewers
 77. video_77        =>    3 viewers
 78. video_78        =>   16 viewers
 79. video_79        =>   13 viewers
 80. video_80        =>    1 viewers
 81. video_81        =>    7 viewers
 82. video_82        =>   14 viewers
 83. video_83        =>    7 viewers
 84. video_84        =>   13 viewers
 85. video_85        =>   17 viewers
 86. video_86        =>   19 viewers
 87. video_87        =>   16 viewers
 88. video_88        =>    4 viewers
 89. video_89        =>   20 viewers
 90. video_90        =>    6 viewers
 91. video_91        =>    6 viewers
 92. video_92        =>   17 viewers
 93. video_93        =>    7 viewers
 94. video_94        =>   20 viewers
 95. video_95        =>   10 viewers
 96. video_96        =>    3 viewers
 97. video_97        =>    1 viewers
 98. video_98        =>    4 viewers
 99. video_99        =>   14 viewers
100. video_100       =>  908 viewers

Viewer Count Distribution:
  0          viewers:   0 videos (0.00%)
  1-10       viewers:  45 videos (45.00%)
  11-20      viewers:  48 videos (48.00%)
  21-50      viewers:   0 videos (0.00%)
  51-100     viewers:   5 videos (5.00%)
  101-200    viewers:   0 videos (0.00%)
  200+       viewers:   2 videos (2.00%)

=== SIMULATING STATEFUL VIDEO-TO-READER DISTRIBUTION ===
Reader API Instances: 5
ReaderApiManager assigns each video to a specific reader API

Assigning videos to reader instances using round-robin...
Assigned 100 videos to 5 reader instances

=== READER API SUBSCRIPTION RESULTS ===

reader_1:
  Total Subscribed Topics: 81
  Total Viewers Served: 2,413

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  210 viewers
      2. video_2         =>   87 viewers
      3. video_3         =>   75 viewers
      4. video_4         =>   71 viewers
      5. video_5         =>   62 viewers
      6. video_6         =>   68 viewers
      7. video_7         =>    9 viewers
      8. video_8         =>    1 viewers
      9. video_9         =>   19 viewers
     10. video_10        =>   19 viewers
     11. video_12        =>   10 viewers
     12. video_13        =>   13 viewers
     13. video_16        =>   10 viewers
     14. video_19        =>   12 viewers
     15. video_20        =>   10 viewers
     16. video_21        =>   10 viewers
     17. video_22        =>   13 viewers
     18. video_23        =>   18 viewers
     19. video_24        =>   11 viewers
     20. video_25        =>    7 viewers
     21. video_26        =>   17 viewers
     22. video_27        =>   13 viewers
     23. video_28        =>   14 viewers
     24. video_29        =>   20 viewers
     25. video_30        =>   14 viewers
     26. video_32        =>   18 viewers
     27. video_35        =>    7 viewers
     28. video_36        =>   12 viewers
     29. video_37        =>   19 viewers
     30. video_38        =>   19 viewers
     31. video_42        =>   18 viewers
     32. video_43        =>   15 viewers
     33. video_44        =>   16 viewers
     34. video_45        =>   19 viewers
     35. video_46        =>   15 viewers
     36. video_48        =>   19 viewers
     37. video_49        =>   10 viewers
     38. video_50        =>    9 viewers
     39. video_52        =>   14 viewers
     40. video_53        =>   20 viewers
     41. video_54        =>   17 viewers
     42. video_55        =>    6 viewers
     43. video_56        =>    5 viewers
     44. video_57        =>    4 viewers
     45. video_59        =>   19 viewers
     46. video_60        =>   15 viewers
     47. video_61        =>   14 viewers
     48. video_62        =>   15 viewers
     49. video_63        =>    9 viewers
     50. video_64        =>    3 viewers
     51. video_65        =>    6 viewers
     52. video_66        =>   16 viewers
     53. video_67        =>    6 viewers
     54. video_69        =>   19 viewers
     55. video_70        =>   20 viewers
     56. video_71        =>    9 viewers
     57. video_72        =>    4 viewers
     58. video_73        =>   14 viewers
     59. video_74        =>   18 viewers
     60. video_75        =>   11 viewers
     61. video_76        =>    8 viewers
     62. video_78        =>   16 viewers
     63. video_79        =>   13 viewers
     64. video_80        =>    1 viewers
     65. video_81        =>    7 viewers
     66. video_82        =>   14 viewers
     67. video_83        =>    7 viewers
     68. video_84        =>   13 viewers
     69. video_85        =>   17 viewers
     70. video_86        =>   19 viewers
     71. video_87        =>   16 viewers
     72. video_89        =>   20 viewers
     73. video_90        =>    6 viewers
     74. video_91        =>    6 viewers
     75. video_92        =>   17 viewers
     76. video_93        =>    7 viewers
     77. video_94        =>   20 viewers
     78. video_95        =>   10 viewers
     79. video_97        =>    1 viewers
     80. video_99        =>   14 viewers
     81. video_100       =>  908 viewers

reader_2:
  Total Subscribed Topics: 82
  Total Viewers Served: 2,436

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  210 viewers
      2. video_2         =>   87 viewers
      3. video_3         =>   75 viewers
      4. video_4         =>   71 viewers
      5. video_5         =>   62 viewers
      6. video_6         =>   68 viewers
      7. video_7         =>    9 viewers
      8. video_9         =>   19 viewers
      9. video_10        =>   19 viewers
     10. video_11        =>   10 viewers
     11. video_12        =>   10 viewers
     12. video_13        =>   13 viewers
     13. video_15        =>    4 viewers
     14. video_16        =>   10 viewers
     15. video_19        =>   12 viewers
     16. video_20        =>   10 viewers
     17. video_21        =>   10 viewers
     18. video_22        =>   13 viewers
     19. video_23        =>   18 viewers
     20. video_24        =>   11 viewers
     21. video_25        =>    7 viewers
     22. video_26        =>   17 viewers
     23. video_27        =>   13 viewers
     24. video_28        =>   14 viewers
     25. video_29        =>   20 viewers
     26. video_30        =>   14 viewers
     27. video_31        =>    3 viewers
     28. video_32        =>   18 viewers
     29. video_33        =>   11 viewers
     30. video_35        =>    7 viewers
     31. video_36        =>   12 viewers
     32. video_37        =>   19 viewers
     33. video_38        =>   19 viewers
     34. video_40        =>    8 viewers
     35. video_42        =>   18 viewers
     36. video_43        =>   15 viewers
     37. video_44        =>   16 viewers
     38. video_45        =>   19 viewers
     39. video_46        =>   15 viewers
     40. video_47        =>    6 viewers
     41. video_48        =>   19 viewers
     42. video_49        =>   10 viewers
     43. video_50        =>    9 viewers
     44. video_52        =>   14 viewers
     45. video_53        =>   20 viewers
     46. video_54        =>   17 viewers
     47. video_55        =>    6 viewers
     48. video_56        =>    5 viewers
     49. video_59        =>   19 viewers
     50. video_60        =>   15 viewers
     51. video_61        =>   14 viewers
     52. video_62        =>   15 viewers
     53. video_63        =>    9 viewers
     54. video_66        =>   16 viewers
     55. video_67        =>    6 viewers
     56. video_69        =>   19 viewers
     57. video_70        =>   20 viewers
     58. video_71        =>    9 viewers
     59. video_72        =>    4 viewers
     60. video_73        =>   14 viewers
     61. video_74        =>   18 viewers
     62. video_75        =>   11 viewers
     63. video_76        =>    8 viewers
     64. video_77        =>    3 viewers
     65. video_78        =>   16 viewers
     66. video_79        =>   13 viewers
     67. video_82        =>   14 viewers
     68. video_84        =>   13 viewers
     69. video_85        =>   17 viewers
     70. video_86        =>   19 viewers
     71. video_87        =>   16 viewers
     72. video_88        =>    4 viewers
     73. video_89        =>   20 viewers
     74. video_90        =>    6 viewers
     75. video_91        =>    6 viewers
     76. video_92        =>   17 viewers
     77. video_93        =>    7 viewers
     78. video_94        =>   20 viewers
     79. video_95        =>   10 viewers
     80. video_98        =>    4 viewers
     81. video_99        =>   14 viewers
     82. video_100       =>  908 viewers

reader_3:
  Total Subscribed Topics: 84
  Total Viewers Served: 2,409

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  210 viewers
      2. video_2         =>   87 viewers
      3. video_3         =>   75 viewers
      4. video_4         =>   71 viewers
      5. video_5         =>   62 viewers
      6. video_6         =>   68 viewers
      7. video_7         =>    9 viewers
      8. video_9         =>   19 viewers
      9. video_10        =>   19 viewers
     10. video_11        =>   10 viewers
     11. video_12        =>   10 viewers
     12. video_13        =>   13 viewers
     13. video_16        =>   10 viewers
     14. video_17        =>    2 viewers
     15. video_19        =>   12 viewers
     16. video_21        =>   10 viewers
     17. video_22        =>   13 viewers
     18. video_23        =>   18 viewers
     19. video_24        =>   11 viewers
     20. video_26        =>   17 viewers
     21. video_27        =>   13 viewers
     22. video_28        =>   14 viewers
     23. video_29        =>   20 viewers
     24. video_30        =>   14 viewers
     25. video_31        =>    3 viewers
     26. video_33        =>   11 viewers
     27. video_34        =>    2 viewers
     28. video_35        =>    7 viewers
     29. video_36        =>   12 viewers
     30. video_37        =>   19 viewers
     31. video_38        =>   19 viewers
     32. video_39        =>    1 viewers
     33. video_40        =>    8 viewers
     34. video_42        =>   18 viewers
     35. video_43        =>   15 viewers
     36. video_44        =>   16 viewers
     37. video_45        =>   19 viewers
     38. video_46        =>   15 viewers
     39. video_47        =>    6 viewers
     40. video_48        =>   19 viewers
     41. video_49        =>   10 viewers
     42. video_51        =>   18 viewers
     43. video_52        =>   14 viewers
     44. video_53        =>   20 viewers
     45. video_54        =>   17 viewers
     46. video_56        =>    5 viewers
     47. video_57        =>    4 viewers
     48. video_58        =>    1 viewers
     49. video_59        =>   19 viewers
     50. video_60        =>   15 viewers
     51. video_61        =>   14 viewers
     52. video_62        =>   15 viewers
     53. video_63        =>    9 viewers
     54. video_64        =>    3 viewers
     55. video_65        =>    6 viewers
     56. video_66        =>   16 viewers
     57. video_67        =>    6 viewers
     58. video_68        =>    3 viewers
     59. video_69        =>   19 viewers
     60. video_70        =>   20 viewers
     61. video_73        =>   14 viewers
     62. video_74        =>   18 viewers
     63. video_75        =>   11 viewers
     64. video_76        =>    8 viewers
     65. video_78        =>   16 viewers
     66. video_79        =>   13 viewers
     67. video_81        =>    7 viewers
     68. video_82        =>   14 viewers
     69. video_83        =>    7 viewers
     70. video_84        =>   13 viewers
     71. video_85        =>   17 viewers
     72. video_86        =>   19 viewers
     73. video_87        =>   16 viewers
     74. video_88        =>    4 viewers
     75. video_89        =>   20 viewers
     76. video_90        =>    6 viewers
     77. video_91        =>    6 viewers
     78. video_92        =>   17 viewers
     79. video_93        =>    7 viewers
     80. video_94        =>   20 viewers
     81. video_95        =>   10 viewers
     82. video_96        =>    3 viewers
     83. video_98        =>    4 viewers
     84. video_100       =>  908 viewers

reader_4:
  Total Subscribed Topics: 77
  Total Viewers Served: 2,391

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  210 viewers
      2. video_2         =>   87 viewers
      3. video_3         =>   75 viewers
      4. video_4         =>   71 viewers
      5. video_5         =>   62 viewers
      6. video_6         =>   68 viewers
      7. video_7         =>    9 viewers
      8. video_9         =>   19 viewers
      9. video_10        =>   19 viewers
     10. video_11        =>   10 viewers
     11. video_12        =>   10 viewers
     12. video_13        =>   13 viewers
     13. video_16        =>   10 viewers
     14. video_17        =>    2 viewers
     15. video_18        =>    2 viewers
     16. video_19        =>   12 viewers
     17. video_20        =>   10 viewers
     18. video_21        =>   10 viewers
     19. video_22        =>   13 viewers
     20. video_23        =>   18 viewers
     21. video_24        =>   11 viewers
     22. video_26        =>   17 viewers
     23. video_27        =>   13 viewers
     24. video_28        =>   14 viewers
     25. video_29        =>   20 viewers
     26. video_30        =>   14 viewers
     27. video_32        =>   18 viewers
     28. video_33        =>   11 viewers
     29. video_37        =>   19 viewers
     30. video_38        =>   19 viewers
     31. video_40        =>    8 viewers
     32. video_42        =>   18 viewers
     33. video_43        =>   15 viewers
     34. video_44        =>   16 viewers
     35. video_45        =>   19 viewers
     36. video_46        =>   15 viewers
     37. video_47        =>    6 viewers
     38. video_48        =>   19 viewers
     39. video_49        =>   10 viewers
     40. video_50        =>    9 viewers
     41. video_51        =>   18 viewers
     42. video_52        =>   14 viewers
     43. video_53        =>   20 viewers
     44. video_54        =>   17 viewers
     45. video_55        =>    6 viewers
     46. video_57        =>    4 viewers
     47. video_59        =>   19 viewers
     48. video_60        =>   15 viewers
     49. video_61        =>   14 viewers
     50. video_62        =>   15 viewers
     51. video_63        =>    9 viewers
     52. video_64        =>    3 viewers
     53. video_65        =>    6 viewers
     54. video_66        =>   16 viewers
     55. video_68        =>    3 viewers
     56. video_69        =>   19 viewers
     57. video_70        =>   20 viewers
     58. video_71        =>    9 viewers
     59. video_73        =>   14 viewers
     60. video_75        =>   11 viewers
     61. video_76        =>    8 viewers
     62. video_78        =>   16 viewers
     63. video_79        =>   13 viewers
     64. video_82        =>   14 viewers
     65. video_83        =>    7 viewers
     66. video_84        =>   13 viewers
     67. video_85        =>   17 viewers
     68. video_86        =>   19 viewers
     69. video_87        =>   16 viewers
     70. video_89        =>   20 viewers
     71. video_90        =>    6 viewers
     72. video_92        =>   17 viewers
     73. video_93        =>    7 viewers
     74. video_94        =>   20 viewers
     75. video_96        =>    3 viewers
     76. video_99        =>   14 viewers
     77. video_100       =>  908 viewers

reader_5:
  Total Subscribed Topics: 83
  Total Viewers Served: 2,435

  All Subscribed Topics (sorted by video ID):
      1. video_1         =>  210 viewers
      2. video_2         =>   87 viewers
      3. video_3         =>   75 viewers
      4. video_4         =>   71 viewers
      5. video_5         =>   62 viewers
      6. video_6         =>   68 viewers
      7. video_7         =>    9 viewers
      8. video_9         =>   19 viewers
      9. video_10        =>   19 viewers
     10. video_11        =>   10 viewers
     11. video_12        =>   10 viewers
     12. video_13        =>   13 viewers
     13. video_14        =>    1 viewers
     14. video_15        =>    4 viewers
     15. video_16        =>   10 viewers
     16. video_19        =>   12 viewers
     17. video_20        =>   10 viewers
     18. video_21        =>   10 viewers
     19. video_22        =>   13 viewers
     20. video_23        =>   18 viewers
     21. video_24        =>   11 viewers
     22. video_25        =>    7 viewers
     23. video_26        =>   17 viewers
     24. video_27        =>   13 viewers
     25. video_28        =>   14 viewers
     26. video_29        =>   20 viewers
     27. video_30        =>   14 viewers
     28. video_31        =>    3 viewers
     29. video_32        =>   18 viewers
     30. video_33        =>   11 viewers
     31. video_35        =>    7 viewers
     32. video_36        =>   12 viewers
     33. video_37        =>   19 viewers
     34. video_38        =>   19 viewers
     35. video_40        =>    8 viewers
     36. video_41        =>    1 viewers
     37. video_42        =>   18 viewers
     38. video_43        =>   15 viewers
     39. video_44        =>   16 viewers
     40. video_45        =>   19 viewers
     41. video_46        =>   15 viewers
     42. video_48        =>   19 viewers
     43. video_49        =>   10 viewers
     44. video_50        =>    9 viewers
     45. video_51        =>   18 viewers
     46. video_52        =>   14 viewers
     47. video_53        =>   20 viewers
     48. video_54        =>   17 viewers
     49. video_56        =>    5 viewers
     50. video_59        =>   19 viewers
     51. video_60        =>   15 viewers
     52. video_61        =>   14 viewers
     53. video_63        =>    9 viewers
     54. video_66        =>   16 viewers
     55. video_67        =>    6 viewers
     56. video_68        =>    3 viewers
     57. video_69        =>   19 viewers
     58. video_70        =>   20 viewers
     59. video_71        =>    9 viewers
     60. video_72        =>    4 viewers
     61. video_73        =>   14 viewers
     62. video_74        =>   18 viewers
     63. video_75        =>   11 viewers
     64. video_76        =>    8 viewers
     65. video_77        =>    3 viewers
     66. video_78        =>   16 viewers
     67. video_79        =>   13 viewers
     68. video_81        =>    7 viewers
     69. video_82        =>   14 viewers
     70. video_84        =>   13 viewers
     71. video_85        =>   17 viewers
     72. video_86        =>   19 viewers
     73. video_87        =>   16 viewers
     74. video_88        =>    4 viewers
     75. video_89        =>   20 viewers
     76. video_90        =>    6 viewers
     77. video_91        =>    6 viewers
     78. video_92        =>   17 viewers
     79. video_93        =>    7 viewers
     80. video_94        =>   20 viewers
     81. video_95        =>   10 viewers
     82. video_99        =>   14 viewers
     83. video_100       =>  908 viewers

=== OVERALL STATISTICS ===
Total Unique Subscriptions: 407
Average Subscriptions per Reader: 81.40

Subscription Distribution:
  Max Subscriptions: 84
  Min Subscriptions: 77
  Standard Deviation: 2.42
  Balance Factor: 8.60%

=== SIMULATION COMPLETE ===
Total execution time: 0.08 seconds