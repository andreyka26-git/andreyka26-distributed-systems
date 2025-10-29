/**
 * In-Memory Simulation: Live Comments Distribution (Small Scale)
 * Simulates Facebook/YouTube/Twitch/TikTok live comments viewer distribution
 * across multiple reader API instances using pub/sub pattern
 * 100 concurrent videos/streams, 1000 concurrent viewers
 */

const { SimulationUtils } = require('../utils');

const NUM_VIDEOS = 100;
const TOTAL_VIDEOS = NUM_VIDEOS;
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
            // IMPORTANT: assigning randomly
            const readerIndex = Math.floor(Math.random() * READER_API_INSTANCES);
            const readerInstanceId = `reader_${readerIndex + 1}`;
            
            viewerToReader.set(viewerId, readerInstanceId);
            readerSubscriptions.get(readerInstanceId).add(videoId);
            
            processedViewers++;
        }
    }

    SimulationUtils.printProcessedViewers(processedViewers);

    return { readerSubscriptions, viewerToReader };
}

function runSimulation() {
    const startTime = Date.now();

    const videoViewers = generateViewerDistribution();
    SimulationUtils.printViewerDistribution(videoViewers);

    const { readerSubscriptions, viewerToReader } = simulatePubSubDistribution(videoViewers);
    SimulationUtils.printReaderSubscriptions(readerSubscriptions, videoViewers, READER_API_INSTANCES);

    SimulationUtils.printSimulationComplete(startTime);
}

runSimulation();
