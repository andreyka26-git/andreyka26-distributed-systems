using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface ILoadBalancerNodeClient
    {
        Task AddVirtualNodeAsync(VirtualNode newVirtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
        Task RemoveVirtualNodeAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken);
    }
}
