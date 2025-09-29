using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface IChildNodeClient
    {
        Task<ChildInformationModel> GetChildClusterInformationModelAsync(PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task RemoveFirstHalfOfCache(uint lastItemToRemoveInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken);
        Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken);
    }
}
