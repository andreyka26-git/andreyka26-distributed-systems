using System.Collections.Concurrent;
using RateLimiter.Api.Application;
using RateLimiter.Api.Domain;

namespace RateLimiter.Api.Infrastructure;

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly IConfiguration _configuration;
    private ConcurrentDictionary<string, CallerInfo> _callers = new();

    public InMemoryRateLimiter(IConfiguration config)
    {
        _configuration = config;
    }
    
    public async Task<bool> AllowAsync(DateTime requestTime, string callerId)
    {
        if (!_callers.TryGetValue(callerId, out var callerInfo))
        {
            var newCallerInfo = new CallerInfo(callerId)
            {
                RateLimitBucketStart = requestTime,
            };
            
            newCallerInfo.IncrementRequestCountSafe();
            _callers.TryAdd(callerId, newCallerInfo);
            
            return true;
        }

        var allowedTimeWindow = _configuration.GetValue<TimeSpan>("AllowedRateLimitWindow");

        if (requestTime - callerInfo.RateLimitBucketStart >= allowedTimeWindow)
        {
            lock (callerId)
            {
                
            }
        }
        
        callerInfo.IncrementRequestCountSafe();
        return true;
    }
}