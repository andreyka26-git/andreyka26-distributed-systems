using DistributedCache.Common.NodeManagement;
using DistributedCache.Common;

namespace DistributedCache.LoadBalancer
{
    public interface IChildNodeService
    {
        Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
    }
}
