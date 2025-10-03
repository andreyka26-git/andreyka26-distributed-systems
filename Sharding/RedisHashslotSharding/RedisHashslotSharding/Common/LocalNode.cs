namespace RedisHashslotSharding.Common;

/// <summary>
/// Local cache, therefore has cache values that can handle
/// </summary>
public class LocalNode : NodeBase
{
    public override bool IsNodeLocal() => true;

    public Dictionary<int, InMemoryCache> LocalHashSlots { get; } = new();
}
