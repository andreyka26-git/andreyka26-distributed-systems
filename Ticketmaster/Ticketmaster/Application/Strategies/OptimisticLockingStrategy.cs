using Dapper;
using Microsoft.EntityFrameworkCore;
using Ticketmaster.Application.Models;
using Ticketmaster.Domain.Entities;
using Ticketmaster.Infrastructure.ExternalServices;
using Ticketmaster.Infrastructure.Persistence;

namespace Ticketmaster.Application.Strategies;

public class OptimisticLockingStrategy : IBookingStrategy
{
    private readonly TicketmasterDbContext _context;
    private readonly IStripeClient _stripeClient;

    public OptimisticLockingStrategy(TicketmasterDbContext context, IStripeClient stripeClient)
    {
        _context = context;
        _stripeClient = stripeClient;
    }

    public async Task<BookingResult> BookSeatAsync(int seatId, string userId)
    {
        var connection = _context.Database.GetDbConnection();
        
        var rowsAffected = await connection.ExecuteAsync(
            "UPDATE \"Seats\" SET \"Status\" = @NewStatus, \"UpdatedAt\" = @UpdatedAt WHERE \"Id\" = @SeatId AND \"Status\" = @CurrentStatus",
            new { NewStatus = "locked", UpdatedAt = DateTime.UtcNow, SeatId = seatId, CurrentStatus = "free" });
        
        if (rowsAffected == 0)
            return new BookingResult { Success = false, Message = "Seat already taken", UserId = userId };

        var seat = await connection.QueryFirstOrDefaultAsync<Seat>(
            "SELECT * FROM \"Seats\" WHERE \"Id\" = @SeatId",
            new { SeatId = seatId });
        
        if (seat == null)
            return new BookingResult { Success = false, Message = "Seat not found", UserId = userId };

        var charged = await _stripeClient.ChargeAsync(seat.Price, userId);
        
        if (!charged)
            return new BookingResult { Success = false, Message = "Payment failed", UserId = userId };

        await connection.ExecuteAsync(
            "UPDATE \"Seats\" SET \"UserId\" = @UserId, \"Status\" = @Status, \"UpdatedAt\" = @UpdatedAt WHERE \"Id\" = @SeatId",
            new { UserId = userId, Status = "booked", UpdatedAt = DateTime.UtcNow, SeatId = seatId });

        return new BookingResult { Success = true, Message = "Booking successful", UserId = userId };
    }
}
