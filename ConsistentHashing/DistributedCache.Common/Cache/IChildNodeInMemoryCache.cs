namespace DistributedCache.Common.Cache
{
    public interface IChildNodeInMemoryCache
    {
        void AddToCache(uint keyHash, string value);
        void RemoveFromCache(uint keyHash);
        Dictionary<uint, string> GetFirstHalfOfCache();
        void RemoveFirstHalfOfCache(uint lastKeyHashInclusively);
        int GetCountOfItems();
        string GetFromCache(uint keyHash);
    }
}
