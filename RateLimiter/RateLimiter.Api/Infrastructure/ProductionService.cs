using System.Collections.Concurrent;
using System.Text.Json;

namespace RateLimiter.Api.Infrastructure;

public class ProductionService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, int>> _requestsPerCaller = new();

    public Task PerformRequest(string callerId, DateTime requestTime)
    {
        long timestamp = new DateTimeOffset(requestTime).ToUnixTimeSeconds();

        var perSecondCounter = _requestsPerCaller.GetOrAdd(callerId, _ => new ConcurrentDictionary<long, int>());

        perSecondCounter.AddOrUpdate(timestamp, 1, (_, count) => count + 1);

        return Task.CompletedTask;
    }
    
    public string GetSerializedRequests()
    {
        return JsonSerializer.Serialize(_requestsPerCaller);
    }
}
