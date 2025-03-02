using System.Collections.Concurrent;
using RateLimiter.Api.Application;
using RateLimiter.Api.Domain;

namespace RateLimiter.Api.Infrastructure;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, CallerInfo> _callerRateLimits = new();

    public InMemoryRateLimiter(IConfiguration config)
    {
        _configuration = config;
    }
    
    public async Task AllowAsync(string callerId, DateTime requestTime)
    {
        var rateLimitPerBucket = _configuration.GetValue<int>("RateLimitPerBucket");
        var allowedTimeWindow = _configuration.GetValue<TimeSpan>("AllowedRateLimitWindow");
        
        var callerInfo = _callerRateLimits.GetOrAdd(callerId, new CallerInfo(callerId, requestTime));

        if (requestTime - callerInfo.RateLimitBucketStart >= allowedTimeWindow)
        {
            //TODO add lock
            callerInfo.ResetBucket(requestTime);
        }
        
        callerInfo.IncrementRequestCountSafe(rateLimitPerBucket);
    }
}