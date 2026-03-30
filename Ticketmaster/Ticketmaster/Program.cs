using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Ticketmaster.Application.Models;
using Ticketmaster.Application.Strategies;
using Ticketmaster.Domain.Entities;
using Ticketmaster.Infrastructure.ExternalServices;
using Ticketmaster.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TicketmasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

var redisConnection = await ConnectionMultiplexer.ConnectAsync(builder.Configuration["Redis:ConnectionString"]!);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

builder.Services.AddSingleton<IStripeClient, MockedStripeClient>();
builder.Services.AddSingleton<IRedisService, RedisService>();

builder.Services.AddScoped<NoLockingStrategy>();
builder.Services.AddScoped<PessimisticLockingStrategy>();
builder.Services.AddScoped<OptimisticLockingStrategy>();
builder.Services.AddScoped<RedLockStrategy>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TicketmasterDbContext>();
    await context.Database.MigrateAsync();

    if (!await context.Events.AnyAsync())
    {
        var eventEntity = new Event
        {
            Location = "Madison Square Garden",
            Performer = "The Rolling Stones",
            DateTime = DateTime.UtcNow.AddMonths(3)
        };
        context.Events.Add(eventEntity);
        await context.SaveChangesAsync();

        for (int i = 1; i <= 5; i++)
        {
            context.Seats.Add(new Seat
            {
                EventId = eventEntity.Id,
                Price = 100 + (i * 10),
                Status = "free",
                UserId = null,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/book/no-lock", async (BookingRequest request, IServiceProvider sp) =>
{
    using var scope1 = sp.CreateScope();
    using var scope2 = sp.CreateScope();
    
    var strategy1 = scope1.ServiceProvider.GetRequiredService<NoLockingStrategy>();
    var strategy2 = scope2.ServiceProvider.GetRequiredService<NoLockingStrategy>();
    
    var task1 = strategy1.BookSeatAsync(request.SeatId, "user1");
    var task2 = strategy2.BookSeatAsync(request.SeatId, "user2");
    
    await Task.WhenAll(task1, task2);
    
    return new { result1 = task1.Result, result2 = task2.Result };
}).WithOpenApi();

app.MapPost("/book/pessimistic", async (BookingRequest request, IServiceProvider sp) =>
{
    using var scope1 = sp.CreateScope();
    using var scope2 = sp.CreateScope();
    
    var strategy1 = scope1.ServiceProvider.GetRequiredService<PessimisticLockingStrategy>();
    var strategy2 = scope2.ServiceProvider.GetRequiredService<PessimisticLockingStrategy>();
    
    var task1 = strategy1.BookSeatAsync(request.SeatId, "user1");
    var task2 = strategy2.BookSeatAsync(request.SeatId, "user2");
    
    await Task.WhenAll(task1, task2);
    
    return new { result1 = task1.Result, result2 = task2.Result };
}).WithOpenApi();

app.MapPost("/book/optimistic", async (BookingRequest request, IServiceProvider sp) =>
{
    using var scope1 = sp.CreateScope();
    using var scope2 = sp.CreateScope();
    
    var strategy1 = scope1.ServiceProvider.GetRequiredService<OptimisticLockingStrategy>();
    var strategy2 = scope2.ServiceProvider.GetRequiredService<OptimisticLockingStrategy>();
    
    var task1 = strategy1.BookSeatAsync(request.SeatId, "user1");
    var task2 = strategy2.BookSeatAsync(request.SeatId, "user2");
    
    await Task.WhenAll(task1, task2);
    
    return new { result1 = task1.Result, result2 = task2.Result };
}).WithOpenApi();

app.MapPost("/book/redlock", async (BookingRequest request, IServiceProvider sp) =>
{
    using var scope1 = sp.CreateScope();
    using var scope2 = sp.CreateScope();
    
    var strategy1 = scope1.ServiceProvider.GetRequiredService<RedLockStrategy>();
    var strategy2 = scope2.ServiceProvider.GetRequiredService<RedLockStrategy>();
    
    var task1 = strategy1.BookSeatAsync(request.SeatId, "user1");
    var task2 = strategy2.BookSeatAsync(request.SeatId, "user2");
    
    await Task.WhenAll(task1, task2);
    
    return new { result1 = task1.Result, result2 = task2.Result };
}).WithOpenApi();

app.Run();