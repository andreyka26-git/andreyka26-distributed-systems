using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Cache
{
    public interface IChildNodeInMemoryCache : IDisposable
    {
        VirtualNode Node { get; }
        Dictionary<uint, string> Cache { get; }
        bool AddToCache(uint keyHash, string value);
        string GetFromCache(uint keyHash);
        void RemoveFromCache(uint keyHash);
        int GetCountOfItems();
        void AddBulkToCache(Dictionary<uint, string> cacheItems);
        Dictionary<uint, string> GetFirstHalfOfCache();
        void RemoveFirstHalfOfCache(uint lastItemToRemoveInclusively);
    }
}
