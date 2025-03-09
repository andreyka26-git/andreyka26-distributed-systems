using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using RateLimiter.Api.Application;

namespace RateLimiter.Api.Infrastructure;

public class AtomicCounter
{
    private int _value;
    public int Value => _value;
    public int Increment() => Interlocked.Increment(ref _value);
}

public class InMemoryProductionService : IProductionService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, (AtomicCounter Successful, AtomicCounter Unsuccessful)>> _requestsPerCaller = new();

    public Task PerformRequest(string callerId, DateTime requestTime)
    {
        var timestamp = new DateTimeOffset(requestTime).ToUnixTimeSeconds();

        var perSecondCounter = _requestsPerCaller
            .GetOrAdd(callerId, _ => new ConcurrentDictionary<long, (AtomicCounter Successful, AtomicCounter Unsuccessful)>());

        var (successful, _) = perSecondCounter.GetOrAdd(timestamp, _ => (new AtomicCounter(), new AtomicCounter()));
        successful.Increment();

        return Task.CompletedTask;
    }

    public Task PerformThrottledRequest(string callerId, DateTime requestTime)
    {
        long timestamp = new DateTimeOffset(requestTime).ToUnixTimeSeconds();

        var perSecondCounter = _requestsPerCaller.GetOrAdd(callerId, _ => new ConcurrentDictionary<long, (AtomicCounter Successful, AtomicCounter Unsuccessful)>());

        var (_, unsuccessful) = perSecondCounter.GetOrAdd(timestamp, _ => (new AtomicCounter(), new AtomicCounter()));

        unsuccessful.Increment();
        return Task.CompletedTask;
    }

    public async Task<string> GetSerializedRequests()
    {
        var result = new Dictionary<string, Dictionary<long, string>>();

        foreach (var caller in _requestsPerCaller)
        {
            var callerStats = new Dictionary<long, string>();

            foreach (var timestampStats in caller.Value)
            {
                // Explicitly access the value of AtomicCounter and store them as ints
                callerStats[timestampStats.Key] =
                    $"success: {timestampStats.Value.Successful.Value}, unsuccessful: {timestampStats.Value.Unsuccessful.Value}";
            }

            result[caller.Key] = callerStats;
        }

        return JsonSerializer.Serialize(result);
    }
}