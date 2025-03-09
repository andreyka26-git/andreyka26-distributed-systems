using System.Text.Json;
using StackExchange.Redis;
using RateLimiter.Api.Application;

namespace RateLimiter.Api.Infrastructure;

public class RedisProductionService : IProductionService
{
    private readonly IDatabase _redisDb;
    private readonly string _keyPrefix = "RateLimiter";

    public RedisProductionService(IConnectionMultiplexer redis)
    {
        _redisDb = redis.GetDatabase();
    }

    public async Task PerformRequest(string callerId, DateTime requestTime)
    {
        // Use a simpler key format without colons in the middle
        // Encode callerId to handle special characters
        var sanitizedCallerId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(callerId));
        var timestamp = new DateTimeOffset(requestTime).ToUnixTimeSeconds();
        var key = $"{_keyPrefix}_{sanitizedCallerId}_{timestamp}";

        await _redisDb.HashIncrementAsync(key, "successful", 1);
        await _redisDb.KeyExpireAsync(key, TimeSpan.FromMinutes(5)); // Set expiry to avoid unnecessary data buildup
    }

    public async Task PerformThrottledRequest(string callerId, DateTime requestTime)
    {
        var sanitizedCallerId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(callerId));
        var timestamp = new DateTimeOffset(requestTime).ToUnixTimeSeconds();
        var key = $"{_keyPrefix}_{sanitizedCallerId}_{timestamp}";

        await _redisDb.HashIncrementAsync(key, "unsuccessful", 1);
        await _redisDb.KeyExpireAsync(key, TimeSpan.FromMinutes(5));
    }

    public async Task<string> GetSerializedRequests()
    {
        var server = _redisDb.Multiplexer.GetServer(_redisDb.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{_keyPrefix}_*").ToArray();

        var result = new Dictionary<string, Dictionary<long, Dictionary<string, long>>>();

        foreach (var key in keys)
        {
            var keyString = key.ToString();
            var parts = keyString.Split('_');

            if (parts.Length != 3)
            {
                Console.WriteLine($"Skipping key with invalid format: {keyString}");
                continue;
            }

            var encodedCallerId = parts[1];
            var callerId = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedCallerId));

            if (!long.TryParse(parts[2], out long timestamp))
            {
                Console.WriteLine($"Skipping key with invalid timestamp: {keyString}");
                continue;
            }

            var values = await _redisDb.HashGetAllAsync(key);

            if (!result.ContainsKey(callerId))
            {
                result[callerId] = new Dictionary<long, Dictionary<string, long>>();
            }

            if (!result[callerId].ContainsKey(timestamp))
            {
                result[callerId][timestamp] = new Dictionary<string, long>();
            }

            foreach (var entry in values)
            {
                if (long.TryParse(entry.Value, out long count))
                {
                    result[callerId][timestamp][entry.Name.ToString()] = count;
                }
            }
        }

        return JsonSerializer.Serialize(result);
    }
}
