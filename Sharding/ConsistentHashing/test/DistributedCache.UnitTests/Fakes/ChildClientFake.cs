using DistributedCache.ChildNode;
using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests.Fakes
{
    internal class ChildClientFake : IChildNodeClient
    {
        public readonly Dictionary<PhysicalNode, ChildNodeService> ChildNodeToServiceMapping = new Dictionary<PhysicalNode, ChildNodeService>();

        public ChildClientFake()
        {
        }

        public async Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].AddBulkToCacheAsync(virtualNode.RingPosition, cacheItems, cancellationToken);
        }   

        public async Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].AddNodeAsync(virtualNode, cancellationToken);
        }

        public async Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].AddValueAsync(addModel.VirtualNode.RingPosition, addModel.KeyHash, addModel.Value, cancellationToken);
        }

        public async Task<ChildInformationModel> GetChildClusterInformationModelAsync(PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var info = await ChildNodeToServiceMapping[physicalNode].GetChildClusterInformationModelAsync(cancellationToken);
            return info;
        }

        public async Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var count = await ChildNodeToServiceMapping[physicalNode].GetCountAsync(virtualNode.RingPosition, cancellationToken);
            return count;
        }

        public async Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var firstHalf = await ChildNodeToServiceMapping[physicalNode].GetFirstHalfOfCacheAsync(virtualNode.RingPosition, cancellationToken);
            return firstHalf;
        }

        public async Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var value = await ChildNodeToServiceMapping[physicalNode].GetValueAsync(virtualNode.RingPosition, keyHash, cancellationToken);
            return value;
        }

        public async Task RemoveFirstHalfOfCache(uint lastItemToRemoveInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].RemoveFirstHalfOfCacheAsync(virtualNode.RingPosition, lastItemToRemoveInclusively, cancellationToken);
        }

        public async Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].RemoveNodeAsync(virtualNode.RingPosition, cancellationToken);
        }
    }
}
