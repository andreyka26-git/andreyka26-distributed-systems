using Microsoft.EntityFrameworkCore;
using Ticketmaster.Domain.Entities;
using Ticketmaster.Infrastructure.Persistence.EntityConfigurations;

namespace Ticketmaster.Infrastructure.Persistence;

public class TicketmasterDbContext : DbContext
{
    public TicketmasterDbContext(DbContextOptions<TicketmasterDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfiguration(new EventConfiguration());
        modelBuilder.ApplyConfiguration(new SeatConfiguration());
    }
}
