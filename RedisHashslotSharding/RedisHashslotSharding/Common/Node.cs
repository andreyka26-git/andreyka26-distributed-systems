namespace RedisHashslotSharding.Common;

public class Node
{
    
    private readonly HashService _hashService;

    public bool Initialized { get; private set; } = false;
    
    public int SnapshotVersion { get; private set; } = 0;
    
    public int NodeId { get; }
    
    public Node(HashService hashService, int nodeId)
    {
        _hashService = hashService;
        
        NodeId = nodeId;
        // NodeId = configuration.GetValue<int>("NodeId");
        HashSlots = new HashSlot[HashService.TotalSlots];

        for (var i = 0; i < HashService.TotalSlots; i++)
        {
            HashSlots[i] = new HashSlot(NodeId);
        }
    }
    
    public HashSlot[] HashSlots { get; }


    public async Task ProcessConfigurationSnapshot(ClusterSnapshot snapshot)
    {
        if (snapshot.Version <= SnapshotVersion)
        {
            return;
        }

        for (var i = 0; i < snapshot.HashSlots.Length; i++)
        {
            // if current Node keeps handling the slot as it was before - don't change anything
            // to not clear the key/value pairs.
            if (HashSlots[i].NodeId == NodeId && snapshot.HashSlots[i] == NodeId)
            {
                continue;
            }
            
            HashSlots[i] = new HashSlot(snapshot.HashSlots[i]);
        }

        SnapshotVersion = snapshot.Version;
        Initialized = true;
    }
    
    public async Task<NodeResponse> SetAsync(string key, string value)
    {
        var slot = GetSlot(key);
        if (slot.NodeId != NodeId)
        {
            return new NodeResponse
            {
                Status = "MOVED",
                Response = slot.NodeId.ToString()
            };
        }
        await slot.SetAsync(key, value);
        
        return new NodeResponse
        {
            Status = "OK"
        };
    }
    
    public async Task<NodeResponse> GetAsync(string key, string value)
    {
        var slot = GetSlot(key);
        
        if (slot.NodeId != NodeId)
        {
            return new NodeResponse
            {
                Status = "MOVED",
                Response = slot.NodeId.ToString()
            };
        }
        var val = await slot.GetAsync(key, value);
        
        return new NodeResponse
        {
            Response = val ?? "EMPTY",
            Status = "OK"
        };
    }

    private HashSlot GetSlot(string key)
    {
        var slotId = _hashService.ComputeSlotId(key);
        if (slotId < 0 || slotId >= HashService.TotalSlots)
        {
            throw new KeyNotFoundException($"Slot with key {slotId} not found");
        }

        return HashSlots[slotId];
    }
}