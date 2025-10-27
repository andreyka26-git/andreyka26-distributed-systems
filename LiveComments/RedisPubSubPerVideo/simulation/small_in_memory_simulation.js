/**
 * In-Memory Simulation: Live Comments Distribution (Small Scale)
 * Simulates Facebook/YouTube/Twitch/TikTok live comments viewer distribution
 * across multiple reader API instances using pub/sub pattern
 * 100 concurrent videos/streams, 1000 concurrent viewers
 */

const TOTAL_VIDEOS = 100;
const TOTAL_VIEWERS = 2500;
const READER_API_INSTANCES = 5;

const TOP_TIER_VIDEOS = 1;
const TOP_TIER_MIN = 200;
const TOP_TIER_MAX = 300;

const SECOND_TIER_VIDEOS = 5;
const SECOND_TIER_MIN = 50;
const SECOND_TIER_MAX = 100;

const LOW_TIER_MIN = 1;
const LOW_TIER_MAX = 20;

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

    console.log(`Creating ${TOP_TIER_VIDEOS} top-tier video (${TOP_TIER_MIN}-${TOP_TIER_MAX} viewers each)...`);
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

    console.log(`Creating ${SECOND_TIER_VIDEOS} second-tier videos (${SECOND_TIER_MIN}-${SECOND_TIER_MAX} viewers each)...`);
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
    console.log(`Creating ${lowTierVideos.toLocaleString()} low-tier videos (${LOW_TIER_MIN}-${LOW_TIER_MAX} viewers each, ${remainingViewers.toLocaleString()} viewers remaining)...`);
    
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

    console.log('Assigning viewers to random reader instances...');
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

    console.log('All Videos (sorted by video ID):');
    sortedVideos.forEach((video, index) => {
        console.log(`${(index + 1).toString().padStart(3)}. ${video.videoId.padEnd(15)} => ${video.count.toString().padStart(4)} viewers`);
    });
    console.log('');

    const buckets = {
        '0': 0,
        '1-10': 0,
        '11-20': 0,
        '21-50': 0,
        '51-100': 0,
        '101-200': 0,
        '200+': 0
    };

    viewerCounts.forEach(count => {
        if (count === 0) buckets['0']++;
        else if (count <= 10) buckets['1-10']++;
        else if (count <= 20) buckets['11-20']++;
        else if (count <= 50) buckets['21-50']++;
        else if (count <= 100) buckets['51-100']++;
        else if (count <= 200) buckets['101-200']++;
        else buckets['200+']++;
    });

    console.log('Viewer Count Distribution:');
    Object.entries(buckets).forEach(([range, count]) => {
        const percentage = (count / sortedVideos.length * 100).toFixed(2);
        console.log(`  ${range.padEnd(10)} viewers: ${count.toString().padStart(3)} videos (${percentage}%)`);
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
        console.log('');
        
        const sortedTopics = Array.from(subscribedTopics)
            .map(videoId => ({
                videoId,
                viewers: videoViewers.get(videoId)?.size || 0
            }))
            .sort((a, b) => a.videoId.localeCompare(b.videoId, undefined, { numeric: true }));

        console.log(`  All Subscribed Topics (sorted by video ID):`);
        sortedTopics.forEach((topic, index) => {
            console.log(`    ${(index + 1).toString().padStart(3)}. ${topic.videoId.padEnd(15)} => ${topic.viewers.toString().padStart(4)} viewers`);
        });
        console.log('');
    });

    const totalSubscriptions = Array.from(readerSubscriptions.values())
        .reduce((sum, topics) => sum + topics.size, 0);
    const avgSubscriptionsPerReader = totalSubscriptions / READER_API_INSTANCES;
    
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
