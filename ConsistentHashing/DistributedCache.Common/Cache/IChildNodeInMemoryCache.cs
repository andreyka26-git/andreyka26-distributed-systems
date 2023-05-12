namespace DistributedCache.Common.Cache
{
    public interface IChildNodeInMemoryCache
    {
        int NodeRingPosition { get; set; }
        bool AddToCache(uint keyHash, string value);
        void AddBulkToCache(Dictionary<uint, string> cacheItems);
        void RemoveFromCache(uint keyHash);
        Dictionary<uint, string> GetFirstHalfOfCache();
        void RemoveFirstHalfOfCache(uint lastKeyHashInclusively);
        int GetCountOfItems();
        string GetFromCache(uint keyHash);
    }
}
