using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketmaster.Domain.Entities;

namespace Ticketmaster.Infrastructure.Persistence.EntityConfigurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.EventId).IsRequired();
        builder.Property(s => s.Price).IsRequired();
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20);
        builder.Property(s => s.UserId).HasMaxLength(100);
        builder.Property(s => s.UpdatedAt).IsRequired();
        
        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
