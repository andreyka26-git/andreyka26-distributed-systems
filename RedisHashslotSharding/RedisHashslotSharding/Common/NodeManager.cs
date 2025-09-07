using System.Text;

namespace RedisHashslotSharding.Common;

// TODO: add gossip protocol
// Add moving hashslot to different nodes
public class NodeManager
{
    private int _nodeIdCounter = 0;
    private readonly HashService _hashService;

    public NodeManager(HashService hashService)
    {
        _hashService = hashService;
    }

    public List<Node> Nodes { get; set; } = new();

    public async Task AddNodeAsync()
    {
        var node = new Node(_hashService, _nodeIdCounter++);
        Nodes.Add(node);
    }

    public async Task FireGossip()
    {
        if (Nodes.Count < 2)
        {
            throw new InvalidOperationException("At least two nodes are required for gossip.");
        }

        var random = new Random();
        var selectedNodes = Nodes.OrderBy(_ => random.Next()).Take(2).ToList();

        var snapshot = new ClusterSnapshot
        {
            Version = selectedNodes.Max(n => n.SnapshotVersion) + 1,
            HashSlots = new int[HashService.TotalSlots]
        };

        foreach (var node in Nodes)
        {
            for (int i = 0; i < node.HashSlots.Length; i++)
            {
                snapshot.HashSlots[i] = node.HashSlots[i].NodeId;
            }
        }

        foreach (var node in selectedNodes)
        {
            await node.ProcessConfigurationSnapshot(snapshot);
        }
    }

    public async Task<string> GetSnapshotAsync()
    {
        var snapshotBuilder = new StringBuilder();

        foreach (var node in Nodes)
        {
            int? rangeStart = null;
            int? rangeEnd = null;

            for (int i = 0; i < node.HashSlots.Length; i++)
            {
                if (node.HashSlots[i].NodeId == node.NodeId)
                {
                    if (rangeStart == null)
                    {
                        rangeStart = i;
                    }

                    rangeEnd = i;
                }
                else if (rangeStart != null)
                {
                    snapshotBuilder.AppendLine($"{rangeStart}-{rangeEnd} -> nodeid{node.NodeId}");
                    rangeStart = null;
                    rangeEnd = null;
                }
            }

            // Append the last range if it exists
            if (rangeStart != null)
            {
                snapshotBuilder.AppendLine($"{rangeStart}-{rangeEnd} -> nodeid{node.NodeId}");
            }
        }

        return snapshotBuilder.ToString();
    }
}