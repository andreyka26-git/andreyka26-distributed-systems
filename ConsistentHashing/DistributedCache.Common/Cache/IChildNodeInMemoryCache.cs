namespace DistributedCache.Common.Cache
{
    public interface IChildNodeInMemoryCache
    {
        Dictionary<uint, string> Cache { get; }
        bool AddToCache(uint keyHash, string value);
        void AddBulkToCache(Dictionary<uint, string> cacheItems);
        void RemoveFromCache(uint keyHash);
        Dictionary<uint, string> GetFirstHalfOfCache(uint nodePosition);
        void RemoveFirstHalfOfCache(uint nodePosition);
        int GetCountOfItems();
        string GetFromCache(uint keyHash);
    }
}
