using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ticketmaster.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TicketmasterDbContext>
{
    public TicketmasterDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TicketmasterDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=ticketmaster;Username=postgres;Password=postgres");
        
        return new TicketmasterDbContext(optionsBuilder.Options);
    }
}
