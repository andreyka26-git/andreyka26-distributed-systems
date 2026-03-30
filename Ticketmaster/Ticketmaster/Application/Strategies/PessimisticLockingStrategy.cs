using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Ticketmaster.Application.Models;
using Ticketmaster.Domain.Entities;
using Ticketmaster.Infrastructure.ExternalServices;
using Ticketmaster.Infrastructure.Persistence;

namespace Ticketmaster.Application.Strategies;

public class PessimisticLockingStrategy : IBookingStrategy
{
    private readonly TicketmasterDbContext _context;
    private readonly IStripeClient _stripeClient;

    public PessimisticLockingStrategy(TicketmasterDbContext context, IStripeClient stripeClient)
    {
        _context = context;
        _stripeClient = stripeClient;
    }

    public async Task<BookingResult> BookSeatAsync(int seatId, string userId)
    {
        var connection = _context.Database.GetDbConnection();
        
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
            
        await using var transaction = await connection.BeginTransactionAsync();
        
        try
        {
            var seat = await connection.QueryFirstOrDefaultAsync<Seat>(
                "SELECT * FROM \"Seats\" WHERE \"Id\" = @SeatId FOR UPDATE",
                new { SeatId = seatId },
                transaction: transaction);
            
            if (seat == null)
            {
                await transaction.RollbackAsync();
                return new BookingResult { Success = false, Message = "Seat not found", UserId = userId };
            }

            if (seat.Status != "free" || seat.UserId != null)
            {
                await transaction.RollbackAsync();
                return new BookingResult { Success = false, Message = "Seat already booked", UserId = userId };
            }

            var charged = await _stripeClient.ChargeAsync(seat.Price, userId);
            
            if (!charged)
            {
                await transaction.RollbackAsync();
                return new BookingResult { Success = false, Message = "Payment failed", UserId = userId };
            }

            await connection.ExecuteAsync(
                "UPDATE \"Seats\" SET \"UserId\" = @UserId, \"Status\" = @Status, \"UpdatedAt\" = @UpdatedAt WHERE \"Id\" = @SeatId",
                new { UserId = userId, Status = "booked", UpdatedAt = DateTime.UtcNow, SeatId = seatId },
                transaction: transaction);
            
            await transaction.CommitAsync();

            return new BookingResult { Success = true, Message = "Booking successful", UserId = userId };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
