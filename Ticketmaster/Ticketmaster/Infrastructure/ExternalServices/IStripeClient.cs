namespace Ticketmaster.Infrastructure.ExternalServices;

public interface IStripeClient
{
    Task<bool> ChargeAsync(int amount, string userId);
}
