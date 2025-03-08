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
        if (requestTime - RateLimitBucketStart >= timeWindow)
        {
            lock (_lock)
            {
                if (requestTime - RateLimitBucketStart >= timeWindow)
                {
                    RateLimitBucketStart = requestTime;
                    Interlocked.Exchange(ref _requestCount, 0);
                }
            }
        }

        if (Interlocked.Increment(ref _requestCount) > maxAllowed)
        {
            Interlocked.Decrement(ref _requestCount);
            throw new RateLimitException($"Rate limit exceeded for caller {Id}");
        }
    }

    public void IncrementRequestCountSafe(int maxAllowed, DateTime requestTime, TimeSpan timeWindow)
    {
        lock (_lock)
        {
            if (requestTime - RateLimitBucketStart >= timeWindow)
            {
                RateLimitBucketStart = requestTime;
                _requestCount = 0;
            }
            _requestCount++;
            
            if (_requestCount > maxAllowed)
            {
                _requestCount--;
                throw new RateLimitException($"Rate limit exceeded for caller {Id}");
            }
        }
    }
}