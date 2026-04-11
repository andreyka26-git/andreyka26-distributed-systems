using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Gateway.Models;

namespace Gateway.Services;

/// <summary>
/// Maintains in-memory state for all WebSocket connections:
///   _userSockets  : userId -> WebSocket
///   _chatUsers    : chatId -> set of userIds (using ConcurrentDictionary as a set)
///   _sendLocks    : userId -> SemaphoreSlim  (one in-flight send per socket)
/// </summary>
public class ChatRoomService
{
    private readonly ConcurrentDictionary<string, WebSocket>          _userSockets = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim>      _sendLocks   = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _chatUsers = new();

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
