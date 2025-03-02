using RateLimiter.Api.Application;

namespace RateLimiter.Api.Domain;

public record CallerInfo
{
    private readonly object _lock = new();
    private string Id { get; init; }
    
    private int _requestCount = 0;
    
    public int RequestsCount => _requestCount;
    
    public DateTime RateLimitBucketStart { get; private set; }
    
    public CallerInfo(string id, DateTime rateLimitBucketStart)
    {
        Id = id;
        RateLimitBucketStart = rateLimitBucketStart;
    }

    public void IncrementRequestCount(int maxAllowed, DateTime requestTime, TimeSpan timeWindow)
    {
        Interlocked.Increment(ref _requestCount);

        if (requestTime - RateLimitBucketStart >= timeWindow)
        {
            lock (_lock)
            {
                RateLimitBucketStart = requestTime;
                Interlocked.Exchange(ref _requestCount, 1);
                return;
            }
        }

        if (Volatile.Read(ref _requestCount) > maxAllowed)
        {
            throw new RateLimitException($"Rate limit exceeded for caller {Id}");
        }
    }

    public void IncrementRequestCountSafe(int maxAllowed, DateTime requestTime, TimeSpan timeWindow)
    {
        lock (_lock)
        {
            _requestCount++;
            
            if (requestTime - RateLimitBucketStart >= timeWindow)
            {
                RateLimitBucketStart = requestTime;
                Interlocked.Exchange(ref _requestCount, 1);
            }

            if (_requestCount > maxAllowed)
            {
                throw new RateLimitException($"Rate limit exceeded for caller {Id}");
            }
        }
    }
}