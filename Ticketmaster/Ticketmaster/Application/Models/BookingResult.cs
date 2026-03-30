namespace Ticketmaster.Application.Models;

public class BookingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
