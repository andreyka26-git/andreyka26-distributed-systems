namespace RedisHashslotSharding.Common;

/// <summary>
/// Can be local node (has cache values), or remote node
/// </summary>
public class NodeBase
{
    public string NodeId { get; set; }

    public string Url { get; set; }

    public virtual bool IsNodeLocal() => false;

    public Dictionary<string, string> Cache => throw new NotImplementedException();
}
