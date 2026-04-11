using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Gateway.Models;

namespace Gateway.Services;

/// <summary>
/// Maintains in-memory state for all WebSocket connections:
///   _userSockets      : userId -> WebSocket
///   _chatUsers        : chatId -> set of userIds (using ConcurrentDictionary as a set)
///   _sendLocks        : userId -> SemaphoreSlim  (one in-flight send per socket)
///   _chatMessageCounts: chatId -> number of messages delivered to that chat
/// </summary>
public class ChatRoomService
{
    private readonly ConcurrentDictionary<string, WebSocket>          _userSockets = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim>      _sendLocks   = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _chatUsers = new();
    private readonly ConcurrentDictionary<string, long>               _chatMessageCounts = new();

    // ----------------------------------------------------------------
    // Connection lifecycle
    // ----------------------------------------------------------------

    public async Task HandleConnectionAsync(string userId, string chatId, WebSocket socket)
    {
        _userSockets[userId] = socket;
        _sendLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        _chatUsers.GetOrAdd(chatId, _ => new ConcurrentDictionary<string, byte>())
                  .TryAdd(userId, 0);

        // Drain incoming frames until the client closes the connection.
        // Clients in this bench don't send data frames — only ping/pong/close.
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed by client",
                        CancellationToken.None);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            _userSockets.TryRemove(userId, out _);
            if (_chatUsers.TryGetValue(chatId, out var users))
                users.TryRemove(userId, out _);
        }
    }

    // ----------------------------------------------------------------
    // Message delivery
    // ----------------------------------------------------------------

    public async Task DeliverMessageAsync(string chatId, MessageRequest req)
    {
        if (!_chatUsers.TryGetValue(chatId, out var userIds) || userIds.IsEmpty)
            return;

        var payload = JsonSerializer.Serialize(new
        {
            chatId,
            message  = req.Message,
            senderId = req.SenderId,
            sentAt   = req.SentAt
        });
        var bytes = Encoding.UTF8.GetBytes(payload);

        // Fan-out to all users in this chat concurrently.
        await Task.WhenAll(userIds.Keys.Select(uid => SendToUserAsync(uid, bytes)));

        _chatMessageCounts.AddOrUpdate(chatId, 1, (_, prev) => prev + 1);
    }

    // ----------------------------------------------------------------
    // Stats
    // ----------------------------------------------------------------

    public void ResetMessageStats() => _chatMessageCounts.Clear();

    public ChatStats GetChatStats()
    {
        var userCounts    = _chatUsers.Values.Select(u => (double)u.Count).OrderBy(x => x).ToList();
        var messageCounts = _chatMessageCounts.Values.Select(v => (double)v).OrderBy(x => x).ToList();

        long totalMessages = _chatMessageCounts.Values.Sum();

        return new ChatStats(
            TotalChats:            _chatUsers.Count,
            UsersPerChat:          ToDistribution(userCounts),
            TotalMessagesProcessed: totalMessages,
            MessagesPerChat:       ToDistribution(messageCounts)
        );
    }

    private static DistributionStats ToDistribution(List<double> sorted)
    {
        if (sorted.Count == 0) return new DistributionStats(0, 0);
        return new DistributionStats(
            P50: Percentile(sorted, 50),
            P99: Percentile(sorted, 99)
        );
    }

    private static double Percentile(List<double> sorted, double p)
    {
        double idx = p / 100.0 * (sorted.Count - 1);
        int lo = (int)idx;
        int hi = Math.Min(lo + 1, sorted.Count - 1);
        return sorted[lo] + (idx - lo) * (sorted[hi] - sorted[lo]);
    }

    // ----------------------------------------------------------------
    // Internal helpers
    // ----------------------------------------------------------------

    private async Task SendToUserAsync(string userId, byte[] bytes)
    {
        if (!_userSockets.TryGetValue(userId, out var socket) ||
            socket.State != WebSocketState.Open)
            return;

        if (!_sendLocks.TryGetValue(userId, out var sem))
            return;

        // Serialize sends for this socket — WebSocket allows only one
        // concurrent SendAsync per instance.
        await sem.WaitAsync();
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        }
        catch (WebSocketException) { }
        finally
        {
            sem.Release();
        }
    }
}
