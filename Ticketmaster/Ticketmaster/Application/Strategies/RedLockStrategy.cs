using Dapper;
using Microsoft.EntityFrameworkCore;
using Ticketmaster.Application.Models;
using Ticketmaster.Domain.Entities;
using Ticketmaster.Infrastructure.ExternalServices;
using Ticketmaster.Infrastructure.Persistence;

namespace Ticketmaster.Application.Strategies;

public class RedLockStrategy : IBookingStrategy
{
    private readonly TicketmasterDbContext _context;
    private readonly IStripeClient _stripeClient;
    private readonly IRedisService _redisService;

    public RedLockStrategy(TicketmasterDbContext context, IStripeClient stripeClient, IRedisService redisService)
    {
        _context = context;
        _stripeClient = stripeClient;
        _redisService = redisService;
    }

    public async Task<BookingResult> BookSeatAsync(int seatId, string userId)
    {
        var lockKey = $"seat:lock:{seatId}";
        var lockAcquired = await _redisService.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(10));
        
        if (!lockAcquired)
            return new BookingResult { Success = false, Message = "Could not acquire lock", UserId = userId };

        var connection = _context.Database.GetDbConnection();
        
        var seat = await connection.QueryFirstOrDefaultAsync<Seat>(
            "SELECT * FROM \"Seats\" WHERE \"Id\" = @SeatId",
            new { SeatId = seatId });
        
        if (seat == null)
            return new BookingResult { Success = false, Message = "Seat not found", UserId = userId };

        if (seat.Status != "free" || seat.UserId != null)
            return new BookingResult { Success = false, Message = "Seat already booked", UserId = userId };

        var charged = await _stripeClient.ChargeAsync(seat.Price, userId);
        
        if (!charged)
            return new BookingResult { Success = false, Message = "Payment failed", UserId = userId };

        await connection.ExecuteAsync(
            "UPDATE \"Seats\" SET \"UserId\" = @UserId, \"Status\" = @Status, \"UpdatedAt\" = @UpdatedAt WHERE \"Id\" = @SeatId",
            new { UserId = userId, Status = "booked", UpdatedAt = DateTime.UtcNow, SeatId = seatId });
        
        await _redisService.MakeLockPermanentAsync(lockKey);

        return new BookingResult { Success = true, Message = "Booking successful", UserId = userId };
    }
}
