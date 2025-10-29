const axios = require('axios');

// ====================================================================
// STATISTICS UTILITIES SECTION
// ====================================================================

/**
 * Utility class for handling statistics operations
 */
class StatisticsUtils {
  /**
   * Send statistics to the statistics API
   * @param {string} statisticsApiUrl - The base URL of the statistics API
   * @param {string} endpoint - The specific endpoint to send to
   * @param {Object} data - The data to send
   * @param {string} logPrefix - Prefix for log messages
   */
  static async sendStatistics(statisticsApiUrl, endpoint, data, logPrefix = '') {
    try {
      await axios.post(`${statisticsApiUrl}${endpoint}`, data);
      
      // Generate a descriptive log message based on the data content
      const logMessage = StatisticsUtils.generateLogMessage(data, endpoint);
      console.log(`${logPrefix} Sent statistics to ${endpoint}: ${logMessage}`);
    } catch (err) {
      console.error(`${logPrefix} Error sending statistics to ${endpoint}:`, err.message);
    }
  }

  /**
   * Generate a descriptive log message based on statistics data
   * @param {Object} data - The statistics data
   * @param {string} endpoint - The endpoint being sent to
   * @returns {string} Formatted log message
   */
  static generateLogMessage(data, endpoint) {
    if (endpoint.includes('comment-api')) {
      return `${data.totalComments || 0} comments, ${(data.activeTopics || []).length} topics`;
    } else if (endpoint.includes('reader-api-statistics')) {
      return `connections=${data.activeConnections || 0}, messages=${data.messagesSent || 0}, topics=${(data.subscribedTopics || []).length}`;
    } else if (endpoint.includes('reader-api-manager')) {
      return `registeredVideos=${data.registeredVideos || 0}, availableReaderApis=${data.availableReaderApis || 0}`;
    } else if (endpoint.includes('client')) {
      return `generated=${data.commentsGenerated || 0}, consumed=${data.commentsConsumed || 0}`;
    }
    return 'data sent';
  }

  /**
   * Aggregate Redis pub/sub channel statistics
   * @param {Object} redisClient - Redis client instance
   * @param {string} pattern - Channel pattern to search for (e.g., 'video:*')
   * @returns {Object} Object containing channels array and channelStats object
   */
  static async aggregateChannelStatistics(redisClient, pattern = 'video:*') {
    try {
      const channels = await redisClient.sendCommand(['PUBSUB', 'CHANNELS', pattern]);
      
      const channelStats = {};
      for (const channel of channels) {
        const [, numSubs] = await redisClient.sendCommand(['PUBSUB', 'NUMSUB', channel]);
        channelStats[channel] = numSubs;
      }

      return { channels, channelStats };
    } catch (err) {
      console.error('Error aggregating channel statistics:', err);
      return { channels: [], channelStats: {} };
    }
  }

  /**
   * Aggregate comments by video from a comments object
   * @param {Object} comments - Comments object with videoid as keys
   * @returns {Object} Object containing commentsByVideo and totalComments
   */
  static aggregateCommentStatistics(comments) {
    const commentsByVideo = {};
    let totalComments = 0;
    
    for (const [videoid, videoComments] of Object.entries(comments)) {
      const count = videoComments.length;
      commentsByVideo[videoid] = count;
      totalComments += count;
    }

    return { commentsByVideo, totalComments };
  }

  /**
   * Retrieve all comments from Redis
   * @param {Object} redisClient - Redis client instance
   * @param {string} pattern - Key pattern to search for (e.g., 'comment:*')
   * @returns {Array} Array of comment objects
   */
  static async retrieveCommentsFromRedis(redisClient, pattern = 'comment:*') {
    const allComments = [];
    try {
      const keys = await redisClient.keys(pattern);
      
      for (const key of keys) {
        const commentData = await redisClient.hGetAll(key);
        if (commentData && Object.keys(commentData).length > 0) {
          allComments.push({ id: key, ...commentData });
        }
      }
      
      console.log(`Retrieved ${allComments.length} comments from Redis`);
    } catch (err) {
      console.error('Error retrieving comments from Redis:', err);
    }
    
    return allComments;
  }

  /**
   * Aggregate all system statistics into a comprehensive JSON response
   * @param {Object} options - Configuration object
   * @param {Object} options.commentApiStats - Comment API statistics
   * @param {Object} options.readerApiStats - Reader API statistics object
   * @param {Object} options.readerApiManagerStats - Reader API Manager statistics (optional)
   * @param {Object} options.clientStats - Client statistics object
   * @param {Array} options.allComments - Array of all comments
   * @returns {Object} Comprehensive statistics object ready for JSON response
   */
  static aggregateSystemStatistics(options) {
    const {
      commentApiStats = {},
      readerApiStats = {},
      readerApiManagerStats,
      clientStats = {},
      allComments = []
    } = options;

    const readers = Object.values(readerApiStats);
    const clients = Object.values(clientStats);

    const result = {
      commentApi: commentApiStats.data || {},
      readerApis: {
        totalReaders: readers.length,
        totalActiveConnections: readers.reduce((sum, r) => sum + r.activeConnections, 0),
        totalMessagesSent: readers.reduce((sum, r) => sum + r.messagesSent, 0),
        allSubscribedTopics: [...new Set(readers.flatMap(r => r.subscribedTopics))],
        readers
      },
      clients: {
        totalClients: clients.length,
        totalCommentsGenerated: clients.reduce((sum, c) => sum + c.commentsGenerated, 0),
        totalCommentsConsumed: clients.reduce((sum, c) => sum + c.commentsConsumed, 0),
        clients
      },
      storedComments: {
        total: allComments.length,
        comments: allComments
      },
      timestamp: new Date().toISOString()
    };

    // Add readerApiManager statistics if provided (for stateful version)
    if (readerApiManagerStats) {
      result.readerApiManager = readerApiManagerStats.data || {};
    }

    return result;
  }
}

// ====================================================================
// SIMULATION UTILITIES SECTION
// ====================================================================

/**
 * Utility class for simulation printing and formatting
 */
class SimulationUtils {
  /**
   * Print viewer distribution statistics
   * @param {Map} videoViewers - Map of videoId to Set of viewers
   */
  static printViewerDistribution(videoViewers) {
    console.log('=== VIEWER DISTRIBUTION STATISTICS ===');
    
    const sortedVideos = Array.from(videoViewers.entries())
        .map(([videoId, viewers]) => ({ videoId, count: viewers.size }))
        .sort((a, b) => a.videoId.localeCompare(b.videoId, undefined, { numeric: true }));

    const viewerCounts = sortedVideos.map(v => v.count);
    const totalViewers = viewerCounts.reduce((sum, count) => sum + count, 0);
    const avgViewers = totalViewers / sortedVideos.length;
    const maxViewers = Math.max(...viewerCounts);
    const minViewers = Math.min(...viewerCounts);

    console.log(`Total Videos: ${sortedVideos.length.toLocaleString()}`);
    console.log(`Total Viewers: ${totalViewers.toLocaleString()}`);
    console.log(`Average Viewers per Video: ${avgViewers.toFixed(2)}`);
    console.log(`Max Viewers: ${maxViewers.toLocaleString()}`);
    console.log(`Min Viewers: ${minViewers}`);
    console.log('');

    // Print top videos
    console.log('Top 20 Most Popular Videos:');
    const topVideos = [...sortedVideos].sort((a, b) => b.count - a.count).slice(0, 20);
    topVideos.forEach((video, index) => {
        console.log(`${(index + 1).toString().padStart(2)}. ${video.videoId.padEnd(20)} => ${video.count.toLocaleString().padStart(7)} viewers`);
    });
    console.log('');

    // Print distribution buckets
    SimulationUtils.printViewerDistributionBuckets(viewerCounts, sortedVideos.length);
  }

  /**
   * Print viewer count distribution buckets
   * @param {Array} viewerCounts - Array of viewer counts
   * @param {number} totalVideos - Total number of videos
   */
  static printViewerDistributionBuckets(viewerCounts, totalVideos) {
    const buckets = {
        '1-100': 0,
        '101-1000': 0,
        '1001-5000': 0,
        '5001-10000': 0,
        '10001-50000': 0,
        '50001+': 0
    };

    viewerCounts.forEach(count => {
        if (count <= 100) buckets['1-100']++;
        else if (count <= 1000) buckets['101-1000']++;
        else if (count <= 5000) buckets['1001-5000']++;
        else if (count <= 10000) buckets['5001-10000']++;
        else if (count <= 50000) buckets['10001-50000']++;
        else buckets['50001+']++;
    });

    console.log('Viewer Count Distribution:');
    Object.entries(buckets).forEach(([range, count]) => {
        const percentage = (count / totalVideos * 100).toFixed(2);
        console.log(`  ${range.padEnd(15)} viewers: ${count.toString().padStart(6)} videos (${percentage}%)`);
    });
    console.log('');
  }

  /**
   * Print reader subscription results and statistics
   * @param {Map} readerSubscriptions - Map of reader ID to Set of subscribed topics
   * @param {Map} videoViewers - Map of videoId to Set of viewers
   * @param {number} readerApiInstances - Number of reader API instances
   */
  static printReaderSubscriptions(readerSubscriptions, videoViewers, readerApiInstances) {
    console.log('=== READER API SUBSCRIPTION RESULTS ===');
    console.log('');

    const totalVideos = videoViewers.size;
    const sortedReaders = Array.from(readerSubscriptions.entries())
        .sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true }));

    // Print per-reader statistics
    sortedReaders.forEach(([readerInstanceId, subscribedTopics]) => {
        const subscriptionPercentage = ((subscribedTopics.size / totalVideos) * 100).toFixed(2);
        
        console.log(`${readerInstanceId}:`);
        console.log(`  Total Subscribed Topics: ${subscribedTopics.size.toLocaleString()} / ${totalVideos.toLocaleString()} videos (${subscriptionPercentage}%)`);
        
        let totalViewers = 0;
        for (const videoId of subscribedTopics) {
            totalViewers += videoViewers.get(videoId)?.size || 0;
        }
        console.log(`  Total Viewers Served: ${totalViewers.toLocaleString()}`);
        
        // Show top topics for this reader
        const topicsByViewers = Array.from(subscribedTopics)
            .map(videoId => ({
                videoId,
                viewers: videoViewers.get(videoId)?.size || 0
            }))
            .sort((a, b) => b.viewers - a.viewers)
            .slice(0, 10);

        console.log(`  Top 10 Most Popular Subscribed Topics:`);
        topicsByViewers.forEach((topic, index) => {
            console.log(`    ${(index + 1).toString().padStart(2)}. ${topic.videoId.padEnd(20)} => ${topic.viewers.toLocaleString().padStart(7)} viewers`);
        });
        console.log('');
    });

    // Print overall statistics
    SimulationUtils.printOverallSubscriptionStatistics(readerSubscriptions, readerApiInstances);
  }

  /**
   * Print overall subscription statistics
   * @param {Map} readerSubscriptions - Map of reader ID to Set of subscribed topics
   * @param {number} readerApiInstances - Number of reader API instances
   */
  static printOverallSubscriptionStatistics(readerSubscriptions, readerApiInstances) {
    const totalSubscriptions = Array.from(readerSubscriptions.values())
        .reduce((sum, topics) => sum + topics.size, 0);
    const avgSubscriptionsPerReader = totalSubscriptions / readerApiInstances;
    
    console.log('=== OVERALL STATISTICS ===');
    console.log(`Total Unique Subscriptions: ${totalSubscriptions.toLocaleString()}`);
    console.log(`Average Subscriptions per Reader: ${avgSubscriptionsPerReader.toFixed(2)}`);
    console.log('');

    const subscriptionCounts = Array.from(readerSubscriptions.values())
        .map(topics => topics.size);
    const maxSubscriptions = Math.max(...subscriptionCounts);
    const minSubscriptions = Math.min(...subscriptionCounts);
    const variance = subscriptionCounts.reduce((sum, count) => {
        return sum + Math.pow(count - avgSubscriptionsPerReader, 2);
    }, 0) / readerApiInstances;
    const stdDev = Math.sqrt(variance);

    console.log('Subscription Distribution:');
    console.log(`  Max Subscriptions: ${maxSubscriptions.toLocaleString()}`);
    console.log(`  Min Subscriptions: ${minSubscriptions.toLocaleString()}`);
    console.log(`  Standard Deviation: ${stdDev.toFixed(2)}`);
    console.log(`  Balance Factor: ${((maxSubscriptions - minSubscriptions) / avgSubscriptionsPerReader * 100).toFixed(2)}%`);
    console.log('');
  }

  /**
   * Print simulation completion summary
   * @param {number} startTime - Simulation start time (Date.now())
   */
  static printSimulationComplete(startTime) {
    const duration = ((Date.now() - startTime) / 1000).toFixed(2);
    console.log('=== SIMULATION COMPLETE ===');
    console.log(`Total execution time: ${duration} seconds`);
    console.log('');
  }

  /**
   * Print a formatted section header
   * @param {string} title - The section title
   */
  static printSectionHeader(title) {
    console.log(`=== ${title.toUpperCase()} ===`);
  }

  /**
   * Print processed viewer count during simulation
   * @param {number} processedViewers - Number of viewers processed
   */
  static printProcessedViewers(processedViewers) {
    console.log(`Processed ${processedViewers.toLocaleString()} viewers`);
    console.log('');
  }

  /**
   * Print detailed reader statistics with video-level breakdown
   * @param {Map} readerSubscriptions - Map of reader ID to Set of subscribed topics
   * @param {Map} readerViewerCounts - Map of reader ID to Map of video stats
   * @param {Map} videoViewers - Map of videoId to Set of viewers
   */
  static printDetailedReaderStatistics(readerSubscriptions, readerViewerCounts, videoViewers) {
    console.log('=== DETAILED READER STATISTICS ===');
    console.log('');

    const sortedReaders = Array.from(readerSubscriptions.entries())
        .sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true }));

    sortedReaders.forEach(([readerInstanceId, subscribedTopics]) => {
        const readerStats = readerViewerCounts.get(readerInstanceId);
        let totalViewersHandled = 0;
        
        console.log(`${readerInstanceId} (${subscribedTopics.size.toLocaleString()} videos):`);
        
        // Get top 15 videos by total viewers for this reader
        const videoStatsList = Array.from(readerStats.entries())
            .map(([videoId, stats]) => ({
                videoId,
                totalViewers: stats.totalViewers,
                handledViewers: stats.handledViewers
            }))
            .sort((a, b) => b.totalViewers - a.totalViewers)
            .slice(0, 15);

        console.log('  Top Videos:');
        videoStatsList.forEach((video, index) => {
            totalViewersHandled += video.handledViewers;
            const padding = (index + 1).toString().padStart(2);
            console.log(`    ${padding}. ${video.videoId.padEnd(20)} => ${video.totalViewers.toLocaleString().padStart(7)} total | ${video.handledViewers.toLocaleString().padStart(7)} handled`);
        });

        // Calculate total for all videos (not just top 15)
        const allTotalViewers = Array.from(readerStats.values())
            .reduce((sum, stats) => sum + stats.handledViewers, 0);

        console.log(`  Total Viewers Handled: ${allTotalViewers.toLocaleString()}`);
        console.log('');
    });
  }
}

module.exports = {
  StatisticsUtils,
  SimulationUtils
};