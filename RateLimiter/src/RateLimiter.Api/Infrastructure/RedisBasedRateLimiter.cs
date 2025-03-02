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

        var windowKey = $"{callerId}:{GetWindowKey(requestTime, allowedTimeWindow)}";

        var requestCount = await _redisDb.StringIncrementAsync(windowKey);

        if (requestCount == 1)
        {
            await _redisDb.KeyExpireAsync(windowKey, allowedTimeWindow);
        }

        _logger.LogInformation($"Caller: {callerId}, Requests: {requestCount}, Window: {allowedTimeWindow}");

        if (requestCount > rateLimitPerBucket)
        {
            _logger.LogWarning($"Rate limit exceeded for {callerId} in the current time window.");
            throw new RateLimitException("Rate limit exceeded.");
        }
    }

    private string GetWindowKey(DateTime requestTime, TimeSpan window)
    {
        var windowStart = requestTime.Ticks / window.Ticks * window.Ticks;
        return windowStart.ToString();
    }
}