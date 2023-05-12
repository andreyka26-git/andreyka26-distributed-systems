using DistributedCache.Common.Cache;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class ChildNodeClientFake : IChildNodeClient
    {
        private readonly Dictionary<VirtualNode, IChildNodeInMemoryCache> _nodeToCacheMapping = new Dictionary<VirtualNode, IChildNodeInMemoryCache>();
        private IMasterNodeClient _masterNodeClient;
        private readonly int _maxItemsPerChildNode;

        public ChildNodeClientFake(
            IMasterNodeClient masterNodeClient,
            int maxItemsPerChildNode)
        {
            _masterNodeClient = masterNodeClient;
            _maxItemsPerChildNode = maxItemsPerChildNode;
        }

        public Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[addModel.VirtualNode];
            cache.AddToCache(addModel.KeyHash, addModel.Value);

            if (cache.GetCountOfItems() >= _maxItemsPerChildNode)
            {
                _masterNodeClient.E
            }

            return Task.CompletedTask;
        }

        public Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[virtualNode];

            var value = cache.GetFromCache(keyHash);

            return Task.FromResult(value);
        }

        public Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[virtualNode];

            var halfOfCache = cache.GetFirstHalfOfCache();

            return Task.FromResult(halfOfCache);
        }

        public Task RemoveFirstHalfOfCache(uint lastKeyHashInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[virtualNode];
            cache.RemoveFirstHalfOfCache(lastKeyHashInclusively);

            return Task.CompletedTask;
        }

        public Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken) 
        {
            var cache = _nodeToCacheMapping[virtualNode];
            cache.AddBulkToCache(cacheItems);

            return Task.CompletedTask;
        }

        public Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[virtualNode];

            var count = cache.GetCountOfItems();

            return Task.FromResult(count);
        }

        public Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Add(virtualNode, new ChildNodeInMemoryCache());
            return Task.CompletedTask;
        }

        public Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            _nodeToCacheMapping.Remove(virtualNode);
            return Task.CompletedTask;
        }
    }
}
