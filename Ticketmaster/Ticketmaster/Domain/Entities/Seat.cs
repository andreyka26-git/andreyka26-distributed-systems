namespace Ticketmaster.Domain.Entities;

public class Seat
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int Price { get; set; }
    public string Status { get; set; } = "free";
    public string? UserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
