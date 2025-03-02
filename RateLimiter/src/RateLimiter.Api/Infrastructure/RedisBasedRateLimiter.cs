using StackExchange.Redis;
using RateLimiter.Api.Application;

namespace RateLimiter.Api.Infrastructure;

public class RedisBasedRateLimiter : IRateLimiter
{
    private readonly IDatabase _redisDb;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisBasedRateLimiter> _logger;

    public RedisBasedRateLimiter(IConfiguration configuration, ILogger<RedisBasedRateLimiter> logger, IConnectionMultiplexer redis)
    {
        _configuration = configuration;
        _logger = logger;
        _redisDb = redis.GetDatabase();
    }

    public async Task AllowAsync(string callerId, DateTime requestTime)
    {
        var rateLimitPerBucket = _configuration.GetValue<int>("RateLimitPerBucket");
        var allowedTimeWindow = _configuration.GetValue<TimeSpan>("AllowedRateLimitWindow");
        
        var windowKey = $"{callerId}:{requestTime:yyyyMMddHHmmss}";
        
        var currentCount = await _redisDb.StringIncrementAsync(windowKey);
        
        if (currentCount == 1)
        {
            await _redisDb.KeyExpireAsync(windowKey, allowedTimeWindow);
        }
        
        if (currentCount > rateLimitPerBucket)
        {
            _logger.LogWarning("Rate limit exceeded for caller {CallerId}", callerId);
            throw new RateLimitException("Rate limit exceeded");
        }
    }
}
