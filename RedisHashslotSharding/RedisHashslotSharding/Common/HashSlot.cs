namespace RedisHashslotSharding.Common;

public class HashSlot
{
    private readonly Dictionary<string, string> _cache = new();
    
    public int NodeId { get; }
    
    // if NodeId is remote node - cache will be empty
    public IReadOnlyDictionary<string, string> Cache => _cache;

    public HashSlot(int nodeId)
    {
        NodeId = nodeId;
    }
    
    public async Task SetAsync(string key, string value)
    {
        _cache[key] = value;
    }
    
    public async Task<string?> GetAsync(string key, string value)
    {
        return _cache.GetValueOrDefault(key);
    }
}