namespace RateLimiter.Api.Application;

public interface IProductionService
{
     public Task<string> GetSerializedRequests();
    
     Task PerformRequest(string callerId, DateTime requestTime);

     Task PerformThrottledRequest(string callerId, DateTime requestTime);
}