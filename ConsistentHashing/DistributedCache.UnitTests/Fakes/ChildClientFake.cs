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

        public Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }   

        public async Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await ChildNodeToServiceMapping[physicalNode].AddNodeAsync(virtualNode, cancellationToken);
        }

        public Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<ChildInformationModel> GetChildClusterInformationModelAsync(PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFirstHalfOfCache(uint lastKeyHashInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
