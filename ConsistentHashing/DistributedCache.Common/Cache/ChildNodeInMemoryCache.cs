using DistributedCache.Common.Cache;

namespace DistributedCache.Common
{
    public class ChildNodeInMemoryCache : IChildNodeInMemoryCache
    {
        private readonly int _maxNodeItemsCount;

        private readonly Dictionary<uint, string> _cache = new Dictionary<uint, string>();
        private readonly SortedList<uint, uint> _sortedCacheHashes = new SortedList<uint, uint>();

        public ChildNodeInMemoryCache(int maxNodeItemsCount)
        {
            _maxNodeItemsCount = maxNodeItemsCount;
        }

        public bool AddToCache(uint keyHash, string value)
        {
            _cache[keyHash] = value;
            _sortedCacheHashes[keyHash] = keyHash;

            if (GetCountOfItems() >= _maxNodeItemsCount)
            {
                return true;
            }

            return false;
        }

        public void AddBulkToCache(Dictionary<uint, string> cacheItems)
        {
            foreach(var kvp in cacheItems)
            {
                if (_cache.ContainsKey(kvp.Key))
                {
                    continue;
                }

                AddToCache(kvp.Key, kvp.Value);
            }
        }

        public void RemoveFromCache(uint keyHash)
        {
            _cache.Remove(keyHash);
            _sortedCacheHashes.Remove(keyHash);
        }

        public Dictionary<uint, string> GetFirstHalfOfCache()
        {
            var halfCount = _cache.Count / 2;
            var firstHalf = _sortedCacheHashes.Take(halfCount);

            var halfDict = new Dictionary<uint, string>(halfCount);

            foreach (var keyHash in firstHalf)
            {
                halfDict.Add(keyHash.Key, _cache[keyHash.Key]);
            }

            return halfDict;
        }

        public void RemoveFirstHalfOfCache(uint lastKeyHashInclusively)
        {
            var hashKeysToRemove = _sortedCacheHashes.Where(k => k.Key <= lastKeyHashInclusively).ToList();

            foreach(var hashKeyToRemove in hashKeysToRemove)
            {
                _sortedCacheHashes.Remove(hashKeyToRemove.Key);
                _cache.Remove(hashKeyToRemove.Key);
            }
        }

        public int GetCountOfItems()
        {
            return _cache.Count;
        }

        public string GetFromCache(uint keyHash)
        {
            if (!_cache.TryGetValue(keyHash, out var value))
            {
                throw new Exception("Not Found");
            }

            return value;
        }
    }
}
