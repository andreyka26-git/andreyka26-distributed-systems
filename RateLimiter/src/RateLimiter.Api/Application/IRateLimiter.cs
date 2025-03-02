namespace RateLimiter.Api.Application;

public interface IRateLimiter
{
    Task AllowAsync(string callerId, DateTime requestTime);
}