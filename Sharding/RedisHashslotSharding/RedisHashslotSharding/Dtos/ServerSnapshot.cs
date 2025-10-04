namespace RedisHashslotSharding.Dtos;

/// <summary>
/// Represents the complete server snapshot
/// </summary>
public class ServerSnapshot
{
    public List<HashSlotSnapshot> NonEmptySlots { get; set; } = new();
    public int EmptySlotCount { get; set; }
    public int NonEmptySlotCount { get; set; }
    public int TotalSlotCount { get; set; }
}