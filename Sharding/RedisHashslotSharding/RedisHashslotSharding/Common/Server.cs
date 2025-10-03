namespace RedisHashslotSharding.Common;

/// <summary>
/// Entry point data structure for current machine
/// </summary>
public class Server
{
    public bool Initialized { get; set; } = false;

    /// <summary>
    ///  2 ^ 14 slots, each slot points to a node (local or remote)
    /// </summary>
    public NodeBase[] HashSlots { get; set; } = new NodeBase[HashService.TotalSlots];

    public async Task InitializeAsSingleInstance(string nodeId, string url)
    {
        var mainNode = new LocalNode
        {
            NodeId = nodeId,
            Url = url,
            
        }
        Initialized = true;
    }

    public async Task InitializeAsClusterNode(string currentNodeId, string currentNodeUrl, List<NodeBase> allNodes)
    {
        
        Initialized = true;
    }

}
