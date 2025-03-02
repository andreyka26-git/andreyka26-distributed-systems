using RateLimiter.Api.Application;

namespace RateLimiter.Api.Domain;

public record CallerInfo
{
    private string Id { get; init; }
    
    private int _requestCount = 0;
    
    public int RequestsCount => _requestCount;
    
    public DateTime RateLimitBucketStart { get; private set; }
    
    public CallerInfo(string id, DateTime rateLimitBucketStart)
    {
        Id = id;
        RateLimitBucketStart = rateLimitBucketStart;
    }

    public void IncrementRequestCountSafe(int maxAllowed)
    {
        if (Volatile.Read(ref _requestCount) >= maxAllowed)
        {
            throw new RateLimitException($"Rate limit exceeded for caller {Id}");
        }
        
        Interlocked.Increment(ref _requestCount);
    }

    public void ResetBucket(DateTime requestTime)
    {
        RateLimitBucketStart = requestTime;
        Interlocked.Exchange(ref _requestCount, 0);
    }
}