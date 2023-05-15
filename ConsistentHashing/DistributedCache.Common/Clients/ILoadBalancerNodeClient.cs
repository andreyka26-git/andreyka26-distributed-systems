using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface ILoadBalancerNodeClient
    {
        Task<LoadBalancerInformationModel> GetLoadBalancerInformationModelAsync(
            PhysicalNode loadBalancerPhysicalNode,
            CancellationToken cancellationToken);

        Task AddVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode newVirtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken);

        Task RemoveVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode virtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken);
    }
}
