using Ticketmaster.Application.Models;

namespace Ticketmaster.Application.Strategies;

public interface IBookingStrategy
{
    Task<BookingResult> BookSeatAsync(int seatId, string userId);
}
