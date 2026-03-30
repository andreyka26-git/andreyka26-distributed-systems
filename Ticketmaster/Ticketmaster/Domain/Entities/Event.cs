namespace Ticketmaster.Domain.Entities;

public class Event
{
    public int Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Performer { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
}
