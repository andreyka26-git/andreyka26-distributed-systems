namespace Ticketmaster.Infrastructure.ExternalServices;

public interface IRedisService
{
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry);
    Task MakeLockPermanentAsync(string key);
}
