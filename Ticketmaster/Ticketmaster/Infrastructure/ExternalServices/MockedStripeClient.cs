namespace Ticketmaster.Infrastructure.ExternalServices;

public class MockedStripeClient : IStripeClient
{
    public Task<bool> ChargeAsync(int amount, string userId)
    {
        return Task.FromResult(true);
    }
}
