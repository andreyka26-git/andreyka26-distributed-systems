namespace RateLimiter.Api.Domain;

public record CallerInfo(string Id)
{
    private int _requestCount = 0;
    public int RequestsCount => _requestCount;
    public DateTime RateLimitBucketStart { get; init; }

    public void IncrementRequestCountSafe()
    {
        Interlocked.Increment(ref _requestCount);
    }
}