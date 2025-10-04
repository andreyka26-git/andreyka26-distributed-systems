namespace RedisHashslotSharding.Dtos;

/// <summary>
/// Represents a snapshot of hash slot data
/// </summary>
public class HashSlotSnapshot
{
    public int SlotId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();
    public int EntryCount { get; set; }
}