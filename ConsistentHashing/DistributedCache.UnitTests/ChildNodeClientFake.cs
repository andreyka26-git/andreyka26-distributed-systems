using DistributedCache.ChildNode.Controllers;
using DistributedCache.Common.Cache;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class ChildNodeClientFake : IChildNodeClient
    {
        public int MaxItemsPerChildNode = 10;
        public readonly Dictionary<VirtualNode, ChildNodeController> _nodeToCacheMapping = new Dictionary<VirtualNode, ChildNodeController>();

        public ChildNodeClientFake()
        {
        }

        public async Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cacheController = _nodeToCacheMapping[addModel.VirtualNode];
            await cacheController.AddValueAsync(addModel.KeyHash, addModel.Value, cancellationToken);
        }

        public async Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cacheController = _nodeToCacheMapping[virtualNode];

            var value = await cacheController.GetValueAsync(keyHash, cancellationToken);
            return value;
        }

        public Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cacheController = _nodeToCacheMapping[virtualNode];

            var halfOfCache = cacheController.GetFirstHalfOfCache();

            return Task.FromResult(halfOfCache);
        }

        public Task RemoveFirstHalfOfCache(uint lastKeyHashInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cacheController = _nodeToCacheMapping[virtualNode];
            cacheController.RemoveFirstHalfOfCache(lastKeyHashInclusively);

            return Task.CompletedTask;
        }

        public Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken) 
        {
            var cacheController = _nodeToCacheMapping[virtualNode];
            cacheController.AddBulkToCache(cacheItems);

            return Task.CompletedTask;
        }

        public Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cacheController = _nodeToCacheMapping[virtualNode];

            var count = cacheController.GetCountOfItems();

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
