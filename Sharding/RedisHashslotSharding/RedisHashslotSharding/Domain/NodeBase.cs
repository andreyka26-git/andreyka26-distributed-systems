namespace RedisHashslotSharding.Domain;

/// <summary>
/// Can be local node (has cache values), or remote node
/// </summary>
public class NodeBase
{
    public required string NodeId { get; set; }

    public required string Url { get; set; }

    public virtual bool IsNodeLocal() => false;

    public virtual Dictionary<int, InMemoryCache> LocalHashSlots => throw new NotImplementedException();
}