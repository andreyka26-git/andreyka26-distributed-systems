using DistributedCache.Common.NodeManagement;
using DistributedCache.Common;
using DistributedCache.Common.Cache;
using DistributedCache.Common.InformationModels;
using DistributedCache.Common.Concurrency;

namespace DistributedCache.ChildNode
{
    public class ChildNodeService : IChildNodeService
    {
        private readonly Dictionary<uint, IChildNodeInMemoryCache> _nodeToCacheMapping =
            new Dictionary<uint, IChildNodeInMemoryCache>();

        public IReadOnlyDictionary<uint, IChildNodeInMemoryCache> NodeToCacheMapping => _nodeToCacheMapping;

        private readonly IRebalancingQueue _rebalancingQueue;

        public ChildNodeService(
            IRebalancingQueue rebalancingQueue)
        {
            _rebalancingQueue = rebalancingQueue;
        }

        public Task<ChildInformationModel> GetChildClusterInformationModelAsync(CancellationToken cancellationToken)
        {
            var model = new ChildInformationModel();

            foreach (var (position, cache) in _nodeToCacheMapping)
            {
                model.VirtualNodesWithItems.Add(new ChildInformationModelItem
                {
                    Node = cache.Node,
                    CacheItems = cache.Cache
                });
            }

            return Task.FromResult(model);
        }

        public Task AddNodeAsync(VirtualNode node, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Add(node.RingPosition, new ThreadSafeChildNodeInMemoryCache(node, new ReadWriteLockService()));
            return Task.CompletedTask;
        }

        public Task RemoveNodeAsync(uint position, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Remove(position);
            return Task.CompletedTask;
        }

        public Task<string> GetValueAsync(uint nodePosition, uint keyHash, CancellationToken cancellationToken)
        {
            var value = _nodeToCacheMapping[nodePosition].GetFromCache(keyHash);
            return Task.FromResult(value);
        }

        public async Task<bool> AddValueAsync(uint nodePosition, uint keyHash, string value, CancellationToken cancellationToken)
        {
            if (!_nodeToCacheMapping.ContainsKey(nodePosition))
            {
                throw new Exception($"there is no node for {nodePosition}, please add virtual node");
            }

            var doesNeedRebalancing = _nodeToCacheMapping[nodePosition].AddToCache(keyHash, value);

            if (doesNeedRebalancing)
            {
                await _rebalancingQueue.EmitNodeRebalancingAsync(_nodeToCacheMapping[nodePosition].Node, cancellationToken);
            }

            return doesNeedRebalancing;
        }

        public Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[nodePosition];

            var firstPart = cache.GetFirstHalfOfCache();
            return Task.FromResult(firstPart);
        }

        public Task RemoveFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[nodePosition];

            cache.RemoveFirstHalfOfCache();
            return Task.CompletedTask;
        }

        public Task AddBulkToCacheAsync(uint nodePosition, Dictionary<uint, string> cacheItems, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[nodePosition];

            cache.AddBulkToCache(cacheItems);
            return Task.CompletedTask;
        }

        public Task<int> GetCountAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var count = _nodeToCacheMapping[nodePosition].GetCountOfItems();
            return Task.FromResult(count);
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

            foreach(var (_, cache) in _nodeToCacheMapping)
            {
                cache.Dispose();
            }
        }
    }
}
