namespace Gateway.Models;

public record MessageRequest(string SenderId, string Message, long SentAt);
