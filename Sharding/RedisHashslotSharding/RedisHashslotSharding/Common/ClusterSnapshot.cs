namespace RedisHashslotSharding.Common;

public class ClusterSnapshot
{
    public int Version { get; set; }
    
    // mapping hashslot -> nodeid
    public int[] HashSlots { get; set; } = new int[HashService.TotalSlots];
}

