using RedisHashslotSharding.Domain;
using RedisHashslotSharding.Dtos;

namespace RedisHashslotSharding.Domain;

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
        };

        for (var i = 0; i < HashService.TotalSlots; i++)
        {
            mainNode.LocalHashSlots.Add(i, new InMemoryCache());
            HashSlots[i] = mainNode; // Assign the node to each slot
        }

        Initialized = true;
    }

    public async Task InitializeAsClusterNode(string currentNodeId, string currentNodeUrl, NodeBase[] hashSlots)
    {
        HashSlots = hashSlots;
        Initialized = true;
    }

    /// <summary>
    /// Gets a snapshot of all hash slots, returning data only for non-empty slots
    /// </summary>
    /// <returns>A snapshot containing non-empty slots and counts of empty/non-empty slots</returns>
    public async Task<ServerSnapshot> GetSnapshotAsync()
    {
        if (!Initialized)
        {
            throw new InvalidOperationException("Server is not initialized");
        }

        var snapshot = new ServerSnapshot
        {
            TotalSlotCount = HashService.TotalSlots
        };

        var nonEmptySlots = new List<HashSlotSnapshot>();
        var emptySlotCount = 0;

        for (int slotId = 0; slotId < HashService.TotalSlots; slotId++)
        {
            var node = HashSlots[slotId];
            if (node == null)
            {
                emptySlotCount++;
                continue;
            }

            // Check if this is a local node with data
            if (node.IsNodeLocal() && node is LocalNode localNode)
            {
                if (localNode.LocalHashSlots.TryGetValue(slotId, out var cache))
                {
                    if (cache.Entries.Count > 0)
                    {
                        // This slot has data
                        var slotSnapshot = new HashSlotSnapshot
                        {
                            SlotId = slotId,
                            NodeId = node.NodeId,
                            NodeUrl = node.Url,
                            Data = new Dictionary<string, string>(cache.Entries),
                            EntryCount = cache.Entries.Count
                        };
                        nonEmptySlots.Add(slotSnapshot);
                    }
                    else
                    {
                        emptySlotCount++;
                    }
                }
                else
                {
                    emptySlotCount++;
                }
            }
            else
            {
                // For remote nodes, we can't directly check if they have data
                // We'll consider them as potentially having data (non-empty)
                // In a real implementation, you might want to make HTTP calls to check
                var slotSnapshot = new HashSlotSnapshot
                {
                    SlotId = slotId,
                    NodeId = node.NodeId,
                    NodeUrl = node.Url,
                    Data = new Dictionary<string, string>(), // Empty for remote nodes
                    EntryCount = 0 // Unknown for remote nodes
                };
                nonEmptySlots.Add(slotSnapshot);
            }
        }

        snapshot.NonEmptySlots = nonEmptySlots;
        snapshot.NonEmptySlotCount = nonEmptySlots.Count;
        snapshot.EmptySlotCount = emptySlotCount;

        return snapshot;
    }
}