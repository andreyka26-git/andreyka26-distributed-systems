namespace RedisHashslotSharding.Domain;

/// <summary>
/// Local cache, therefore has cache values that can handle
/// </summary>
public class LocalNode : NodeBase
{
    public override bool IsNodeLocal() => true;

    public override Dictionary<int, InMemoryCache> LocalHashSlots { get; } = new();
}