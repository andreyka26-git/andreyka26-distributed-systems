using Gateway.Models;
using Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatRoomService>();
builder.Services.AddSingleton<StatsService>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// ----------------------------------------------------------------
// WS /ws?userId=u1&chatId=chat1
// ----------------------------------------------------------------
app.Map("/ws", async (HttpContext context, ChatRoomService rooms) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var userId = context.Request.Query["userId"].FirstOrDefault();
    var chatId = context.Request.Query["chatId"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(chatId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    await rooms.HandleConnectionAsync(userId, chatId, socket);
});

// ----------------------------------------------------------------
// POST /chat/{chatId}/message   — ChatAPI -> GW
// Body: { "senderId": "u1", "message": "hello", "sentAt": 1712345678123 }
// ----------------------------------------------------------------
app.MapPost("/chat/{chatId}/message", async (
    string chatId,
    MessageRequest req,
    ChatRoomService rooms,
    StatsService stats) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await rooms.DeliverMessageAsync(chatId, req);
    sw.Stop();
    stats.Record(sw.Elapsed.TotalMilliseconds);
    return Results.Ok(new { delivered = true });
});

// ----------------------------------------------------------------
// GET /stats   — delivery latency + chat/user/message distribution
// ----------------------------------------------------------------
app.MapGet("/stats", (StatsService stats, ChatRoomService rooms) =>
    Results.Ok(new StatsResponse(
        DeliveryLatency: stats.GetLatencyStats(),
        Chat:            rooms.GetChatStats()
    )));

// ----------------------------------------------------------------
// POST /stats/reset   — clear all accumulated stats
// ----------------------------------------------------------------
app.MapPost("/stats/reset", (StatsService stats, ChatRoomService rooms) =>
{
    stats.Reset();
    rooms.ResetMessageStats();
    return Results.Ok(new { reset = true });
});

app.Run();
