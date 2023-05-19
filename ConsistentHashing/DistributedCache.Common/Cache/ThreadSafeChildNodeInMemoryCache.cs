using DistributedCache.Common.Cache;
using DistributedCache.Common.Concurrency;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    // TODO if we already have write lock aquired and try to get read or another write locks - we will have an exception, we need to handle it.
    public class ThreadSafeChildNodeInMemoryCache : IChildNodeInMemoryCache
    {
        private readonly VirtualNode _node;
        private readonly Dictionary<uint, string> _cache = new Dictionary<uint, string>();
        private readonly SortedList<uint, uint> _sortedInAscCacheHashes = new SortedList<uint, uint>();
        private readonly IReadWriteLockService _lockService;

        public Dictionary<uint, string> Cache => _cache;
        public VirtualNode Node => _node;

        public ThreadSafeChildNodeInMemoryCache(VirtualNode node, IReadWriteLockService lockService)
        {
            _node = node;
            _lockService = lockService;
        }

        public bool AddToCache(uint keyHash, string value)
        {
            var needRebalance = _lockService.Write(() =>
            {
                _cache[keyHash] = value;
                _sortedInAscCacheHashes[keyHash] = keyHash;

                if (GetCountOfItemsNotSafe() >= _node.MaxItemsCount)
                {
                    return true;
                }

                return false;
            });

            return needRebalance;
        }

        public bool AddToCacheNotSafe(uint keyHash, string value)
        {
            _cache[keyHash] = value;
            _sortedInAscCacheHashes[keyHash] = keyHash;

            if (GetCountOfItemsNotSafe() >= _node.MaxItemsCount)
            {
                return true;
            }

            return false;
        }

        public int GetCountOfItems()
        {
            return _lockService.Read(() => _cache.Count);
        }

        public int GetCountOfItemsNotSafe()
        {
            return _cache.Count;
        }

        public string GetFromCache(uint keyHash)
        {
            var value = _lockService.Read(() =>
            {
                if (!_cache.TryGetValue(keyHash, out var value))
                {
                    throw new Exception("Not Found");
                }

                return value;
            });

            return value;
        }

        public void RemoveFromCache(uint keyHash)
        {
            _lockService.Write(() =>
            {
                _cache.Remove(keyHash);
                _sortedInAscCacheHashes.Remove(keyHash);
            });
        }

        public void AddBulkToCache(Dictionary<uint, string> cacheItems)
        {
            _lockService.Write(() =>
            {
                foreach (var kvp in cacheItems)
                {
                    if (_cache.ContainsKey(kvp.Key))
                    {
                        continue;
                    }

                    AddToCacheNotSafe(kvp.Key, kvp.Value);
                }
            });
        }

        public Dictionary<uint, string> GetFirstHalfOfCache()
        {
            var firstHalf = _lockService.Read(() =>
            {
                var halfDict = GetFirstHalfOfCacheNotSafe();

                return halfDict;
            });

            return firstHalf;
        }

        public Dictionary<uint, string> GetFirstHalfOfCacheNotSafe()
        {
            var halfCount = _cache.Count / 2;
            var firstHalf = _sortedInAscCacheHashes.Where(k => k.Key <= _node.RingPosition).Take(halfCount).ToList();

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

        public void RemoveFirstHalfOfCache()
        {
            _lockService.Write(() =>
            {
                var keyHashesToRemove = GetFirstHalfOfCacheNotSafe();

                foreach (var keyHashToRemove in keyHashesToRemove)
                {
                    _sortedInAscCacheHashes.Remove(keyHashToRemove.Key);
                    _cache.Remove(keyHashToRemove.Key);
                }
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _lockService.Dispose();
        }
    }
}
