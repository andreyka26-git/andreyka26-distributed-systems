using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common;
using DistributedCache.Common.Cache;

namespace DistributedCache.ChildNode
{
    public class ChildNodeService : IChildNodeService
    {
        private readonly Dictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> _nodeToCacheMapping =
            new Dictionary<uint, (VirtualNode, IChildNodeInMemoryCache)>();

        public IReadOnlyDictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> NodeToCacheMapping => _nodeToCacheMapping;

        private readonly IRebalancingQueue _rebalancingQueue;

        public ChildNodeService(
            IRebalancingQueue rebalancingQueue)
        {
            _rebalancingQueue = rebalancingQueue;
        }

        public Task AddNodeAsync(VirtualNode node, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Add(node.RingPosition, (node, new ChildNodeInMemoryCache(node.MaxItemsCount)));
            return Task.CompletedTask;
        }

        public Task RemoveNodeAsync(uint position, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Remove(position);
            return Task.CompletedTask;
        }

        public Task<string> GetValueAsync(uint nodePosition, uint hashKey, CancellationToken cancellationToken)
        {
            var value = _nodeToCacheMapping[nodePosition].Cache.GetFromCache(hashKey);
            return Task.FromResult(value);
        }

        public async Task<bool> AddValueAsync(uint nodePosition, uint hashKey, string value, CancellationToken cancellationToken)
        {
            var doesNeedRebalancing = _nodeToCacheMapping[nodePosition].Cache.AddToCache(hashKey, value);

            if (doesNeedRebalancing)
            {
                await _rebalancingQueue.EmitNodeRebalancingAsync(_nodeToCacheMapping[nodePosition].Node, cancellationToken);
            }

            return doesNeedRebalancing;
        }

        public Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var (_, cache) = _nodeToCacheMapping[nodePosition];

            var firstPart = cache.GetFirstHalfOfCache();
            return Task.FromResult(firstPart);
        }

        public Task RemoveFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var (_, cache) = _nodeToCacheMapping[nodePosition];

            cache.RemoveFirstHalfOfCache(nodePosition);
            return Task.CompletedTask;
        }

        public Task AddBulkToCacheAsync(uint nodePosition, Dictionary<uint, string> cacheItems, CancellationToken cancellationToken)
        {
            var (_, cache) = _nodeToCacheMapping[nodePosition];

            cache.AddBulkToCache(cacheItems);
            return Task.CompletedTask;
        }

        public Task<int> GetCountAsync(uint nodePosition, CancellationToken cancellationToken)
        {
            var count = _nodeToCacheMapping[nodePosition].Cache.GetCountOfItems();
            return Task.FromResult(count);
        }
    }
}
