using StackExchange.Redis;

namespace Ticketmaster.Infrastructure.ExternalServices;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync(key, "locked", expiry, When.NotExists);
    }

    public async Task MakeLockPermanentAsync(string key)
    {
        var db = _redis.GetDatabase();
        await db.KeyPersistAsync(key);
    }
}
