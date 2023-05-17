using DistributedCache.Common.Cache;

namespace DistributedCache.Common
{
    public class ChildNodeInMemoryCache : IChildNodeInMemoryCache
    {
        private readonly int _maxNodeItemsCount;

        public Dictionary<uint, string> Cache => _cache;

        private readonly Dictionary<uint, string> _cache = new Dictionary<uint, string>();
        private readonly SortedList<uint, uint> _sortedInAscCacheHashes = new SortedList<uint, uint>();

        public ChildNodeInMemoryCache(int maxNodeItemsCount)
        {
            _maxNodeItemsCount = maxNodeItemsCount;
        }

        public bool AddToCache(uint keyHash, string value)
        {
            _cache[keyHash] = value;
            _sortedInAscCacheHashes[keyHash] = keyHash;

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
            _sortedInAscCacheHashes.Remove(keyHash);
        }

        public Dictionary<uint, string> GetFirstHalfOfCache(uint nodePosition)
        {
            var halfCount = _cache.Count / 2;
            var firstHalf = _sortedInAscCacheHashes.Where(k => k.Key <= nodePosition).Take(halfCount).ToList();

            var tailDelta = halfCount - firstHalf.Count;

            if (tailDelta > 0)
            {
                //add from the tail
                var rest = _sortedInAscCacheHashes.Reverse().Take(tailDelta);
                firstHalf.AddRange(rest);
            }

            var halfDict = new Dictionary<uint, string>(halfCount);

            foreach (var keyHash in firstHalf)
            {
                halfDict.Add(keyHash.Key, _cache[keyHash.Key]);
            }

            return halfDict;
        }

        public void RemoveFirstHalfOfCache(uint nodePosition)
        {
            var keyHashesToRemove = GetFirstHalfOfCache(nodePosition);

            foreach(var keyHashToRemove in keyHashesToRemove)
            {
                _sortedInAscCacheHashes.Remove(keyHashToRemove.Key);
                _cache.Remove(keyHashToRemove.Key);
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
