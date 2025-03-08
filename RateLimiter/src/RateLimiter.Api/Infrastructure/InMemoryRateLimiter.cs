using System.Collections.Concurrent;
using RateLimiter.Api.Application;
using RateLimiter.Api.Domain;

namespace RateLimiter.Api.Infrastructure;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InMemoryRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, CallerInfo> _callerRateLimits = new();

    public InMemoryRateLimiter(IConfiguration config, ILogger<InMemoryRateLimiter> logger)
    {
        _configuration = config;
        _logger = logger;
    }
    
    public async Task AllowAsync(string callerId, DateTime requestTime)
    {
        var rateLimitPerBucket = _configuration.GetValue<int>("RateLimitPerBucket");
        var allowedTimeWindow = _configuration.GetValue<TimeSpan>("AllowedRateLimitWindow");
        
        var callerInfo = _callerRateLimits.GetOrAdd(callerId, new CallerInfo(callerId, requestTime));

        _logger.LogInformation($"Allowed request time: {requestTime}, requests done: {callerInfo.RequestsCount}, allowed: {rateLimitPerBucket}");
        callerInfo.IncrementRequestCountSafe(rateLimitPerBucket, requestTime, allowedTimeWindow);
        // callerInfo.IncrementRequestCount(rateLimitPerBucket, requestTime, allowedTimeWindow);
    }
}