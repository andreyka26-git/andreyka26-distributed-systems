using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketmaster.Domain.Entities;

namespace Ticketmaster.Infrastructure.Persistence.EntityConfigurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Location).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Performer).IsRequired().HasMaxLength(200);
        builder.Property(e => e.DateTime).IsRequired();
    }
}
