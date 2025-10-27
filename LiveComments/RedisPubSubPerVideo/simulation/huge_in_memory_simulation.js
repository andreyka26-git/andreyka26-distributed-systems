/**
 * In-Memory Simulation: Live Comments Distribution
 * Simulates Facebook/YouTube/Twitch/TikTok live comments viewer distribution
 * across multiple reader API instances using pub/sub pattern
 * 90k concurrent videos/streams, 2M concurrent viewers
 */

const TOTAL_VIDEOS = 90000;
const TOTAL_VIEWERS = 2000000;
const READER_API_INSTANCES = 10;

const TOP_TIER_VIDEOS = 10;
const TOP_TIER_MIN = 20000;
const TOP_TIER_MAX = 100000;

const SECOND_TIER_VIDEOS = 100;
const SECOND_TIER_MIN = 1000;
const SECOND_TIER_MAX = 5000;

const LOW_TIER_MIN = 1;
const LOW_TIER_MAX = 500;

function randomInt(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function generateViewerDistribution() {
    console.log('=== GENERATING VIEWER DISTRIBUTION ===');
    console.log(`Total Videos: ${TOTAL_VIDEOS.toLocaleString()}, Total Viewers: ${TOTAL_VIEWERS.toLocaleString()}`);
    console.log('');

    const videoViewers = new Map();
    const allViewers = [];
    
    for (let i = 1; i <= TOTAL_VIEWERS; i++) {
        allViewers.push(`viewer_${i}`);
    }
    
    let viewerIndex = 0;
    let videoIndex = 1;

    console.log(`Creating ${TOP_TIER_VIDEOS} top-tier videos (20k-100k viewers each)...`);
    for (let i = 0; i < TOP_TIER_VIDEOS && viewerIndex < TOTAL_VIEWERS; i++) {
        const viewerCount = Math.min(
            randomInt(TOP_TIER_MIN, TOP_TIER_MAX),
            TOTAL_VIEWERS - viewerIndex
        );
        
        const viewers = new Set();
        for (let j = 0; j < viewerCount && viewerIndex < TOTAL_VIEWERS; j++) {
            viewers.add(allViewers[viewerIndex++]);
        }
        videoViewers.set(`video_${videoIndex++}`, viewers);
    }

    console.log(`Creating ${SECOND_TIER_VIDEOS} second-tier videos (1k-5k viewers each)...`);
    for (let i = 0; i < SECOND_TIER_VIDEOS && viewerIndex < TOTAL_VIEWERS; i++) {
        const viewerCount = Math.min(
            randomInt(SECOND_TIER_MIN, SECOND_TIER_MAX),
            TOTAL_VIEWERS - viewerIndex
        );
        
        const viewers = new Set();
        for (let j = 0; j < viewerCount && viewerIndex < TOTAL_VIEWERS; j++) {
            viewers.add(allViewers[viewerIndex++]);
        }
        videoViewers.set(`video_${videoIndex++}`, viewers);
    }

    const lowTierVideos = TOTAL_VIDEOS - TOP_TIER_VIDEOS - SECOND_TIER_VIDEOS;
    const remainingViewers = TOTAL_VIEWERS - viewerIndex;
    console.log(`Creating ${lowTierVideos.toLocaleString()} low-tier videos (1-500 viewers each, ${remainingViewers.toLocaleString()} viewers remaining)...`);
    
    for (let i = 0; i < lowTierVideos; i++) {
        const isLastVideo = i === lowTierVideos - 1;
        const viewerCount = isLastVideo 
            ? Math.max(0, TOTAL_VIEWERS - viewerIndex)
            : Math.min(
                randomInt(LOW_TIER_MIN, LOW_TIER_MAX),
                Math.max(0, TOTAL_VIEWERS - viewerIndex)
            );
        
        const viewers = new Set();
        for (let j = 0; j < viewerCount && viewerIndex < TOTAL_VIEWERS; j++) {
            viewers.add(allViewers[viewerIndex++]);
        }
        
        // allows some videos to have zero viewers
        if (viewers.size > 0 || i < lowTierVideos - 1) {
            videoViewers.set(`video_${videoIndex++}`, viewers);
        }
    }

    console.log('');
    console.log(`Total viewers assigned: ${viewerIndex.toLocaleString()}`);
    console.log(`Total videos created: ${videoViewers.size.toLocaleString()}`);
    console.log('');

    return videoViewers;
}

function simulatePubSubDistribution(videoViewers) {
    console.log('=== SIMULATING PUB/SUB DISTRIBUTION ===');
    console.log(`Reader API Instances: ${READER_API_INSTANCES}`);
    console.log('');

    const viewerToReader = new Map();
    const readerSubscriptions = new Map();
    
    for (let i = 0; i < READER_API_INSTANCES; i++) {
        readerSubscriptions.set(`reader_${i + 1}`, new Set());
    }

    console.log(`Assigning viewers to random reader instances...`);
    let processedViewers = 0;
    
    for (const [videoId, viewers] of videoViewers.entries()) {
        for (const viewerId of viewers) {
            const readerIndex = Math.floor(Math.random() * READER_API_INSTANCES);
            const readerInstanceId = `reader_${readerIndex + 1}`;
            
            viewerToReader.set(viewerId, readerInstanceId);
            readerSubscriptions.get(readerInstanceId).add(videoId);
            
            processedViewers++;
        }
    }

    console.log(`Processed ${processedViewers.toLocaleString()} viewers`);
    console.log('');

    return { readerSubscriptions, viewerToReader };
}


function printViewerDistribution(videoViewers) {
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

    console.log('Top 20 Most Popular Videos:');
    const topVideos = [...sortedVideos].sort((a, b) => b.count - a.count).slice(0, 20);
    topVideos.forEach((video, index) => {
        console.log(`${(index + 1).toString().padStart(2)}. ${video.videoId.padEnd(20)} => ${video.count.toLocaleString().padStart(7)} viewers`);
    });
    console.log('');

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
        const percentage = (count / sortedVideos.length * 100).toFixed(2);
        console.log(`  ${range.padEnd(15)} viewers: ${count.toString().padStart(6)} videos (${percentage}%)`);
    });
    console.log('');
}

function printReaderSubscriptions(readerSubscriptions, videoViewers) {
    console.log('=== READER API SUBSCRIPTION RESULTS ===');
    console.log('');

    const sortedReaders = Array.from(readerSubscriptions.entries())
        .sort((a, b) => a[0].localeCompare(b[0], undefined, { numeric: true }));

    sortedReaders.forEach(([readerInstanceId, subscribedTopics]) => {
        console.log(`${readerInstanceId}:`);
        console.log(`  Total Subscribed Topics: ${subscribedTopics.size.toLocaleString()}`);
        
        let totalViewers = 0;
        for (const videoId of subscribedTopics) {
            totalViewers += videoViewers.get(videoId)?.size || 0;
        }
        console.log(`  Total Viewers Served: ${totalViewers.toLocaleString()}`);
        
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

    const totalSubscriptions = Array.from(readerSubscriptions.values())
        .reduce((sum, topics) => sum + topics.size, 0);
    const avgSubscriptionsPerReader = totalSubscriptions / READER_API_INSTANCES;
    
    console.log('=== OVERALL STATISTICS ===');
    console.log(`Total Unique Subscriptions: ${totalSubscriptions.toLocaleString()}`);
    console.log(`Average Subscriptions per Reader: ${avgSubscriptionsPerReader.toLocaleString()}`);
    console.log('');

    const subscriptionCounts = Array.from(readerSubscriptions.values())
        .map(topics => topics.size);
    const maxSubscriptions = Math.max(...subscriptionCounts);
    const minSubscriptions = Math.min(...subscriptionCounts);
    const variance = subscriptionCounts.reduce((sum, count) => {
        return sum + Math.pow(count - avgSubscriptionsPerReader, 2);
    }, 0) / READER_API_INSTANCES;
    const stdDev = Math.sqrt(variance);

    console.log('Subscription Distribution:');
    console.log(`  Max Subscriptions: ${maxSubscriptions.toLocaleString()}`);
    console.log(`  Min Subscriptions: ${minSubscriptions.toLocaleString()}`);
    console.log(`  Standard Deviation: ${stdDev.toFixed(2)}`);
    console.log(`  Balance Factor: ${((maxSubscriptions - minSubscriptions) / avgSubscriptionsPerReader * 100).toFixed(2)}%`);
    console.log('');
}

function runSimulation() {
    const startTime = Date.now();

    const videoViewers = generateViewerDistribution();
    printViewerDistribution(videoViewers);

    const { readerSubscriptions, viewerToReader } = simulatePubSubDistribution(videoViewers);
    printReaderSubscriptions(readerSubscriptions, videoViewers);

    const duration = ((Date.now() - startTime) / 1000).toFixed(2);

    console.log('=== SIMULATION COMPLETE ===');
    console.log(`Total execution time: ${duration} seconds`);
    console.log('');
}

runSimulation();
