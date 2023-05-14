using DistributedCache.ChildNode.Controllers;
using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests.Fakes
{
    public class ChildNodeClientFake : IChildNodeClient
    {
        public int MaxItemsPerChildNode = 10;
        private readonly ChildNodeController _childController;

        public ChildNodeClientFake(ChildNodeController childController)
        {
            _childController = childController;
        }

        public async Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await _childController.AddValueAsync(addModel.VirtualNode.RingPosition, addModel.KeyHash, addModel.Value, cancellationToken);
        }

        public async Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var value = await _childController.GetValueAsync(virtualNode.RingPosition, keyHash, cancellationToken);
            return value;
        }

        public async Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var halfOfCache = await _childController.GetFirstHalfOfCacheAsync(virtualNode.RingPosition, cancellationToken);

            return halfOfCache;
        }

        public async Task RemoveFirstHalfOfCache(uint lastKeyHashInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await _childController.RemoveFirstHalfOfCacheAsync(lastKeyHashInclusively, cancellationToken);
        }

        public async Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await _childController.AddBulkToCacheAsync(virtualNode.RingPosition, cacheItems, cancellationToken);
        }

        public Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var count = _childController.GetCountAsync(virtualNode.RingPosition, cancellationToken);
            return count;
        }

        public async Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await _childController.AddNodeAsync(virtualNode, cancellationToken);
        }

        public async Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await _childController.RemoveNodeAsync(virtualNode.RingPosition, cancellationToken);
        }
    }
}
