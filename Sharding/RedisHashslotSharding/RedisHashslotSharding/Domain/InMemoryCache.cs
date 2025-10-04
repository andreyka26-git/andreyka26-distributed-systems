namespace RedisHashslotSharding.Domain;

/// <summary>
/// In-memory cache implementation for storing key-value pairs
/// </summary>
public class InMemoryCache
{
    /// <summary>
    /// Very abstracted, without interface exposure, and only for string -> string
    /// </summary>
    public Dictionary<string, string> Entries { get; } = new();
}