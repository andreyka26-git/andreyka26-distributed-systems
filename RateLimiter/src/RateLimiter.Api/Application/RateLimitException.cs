namespace RateLimiter.Api.Application;

public class RateLimitException : Exception
{
    public RateLimitException(string message) : base(message)
    {
    }
}