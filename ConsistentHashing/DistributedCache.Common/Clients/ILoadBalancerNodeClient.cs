using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface ILoadBalancerNodeClient
    {
        Task AddVirtualNodeAsync(VirtualNode newVirtualNode, CancellationToken cancellationToken);
        Task RemoveVirtualNodeAsync(VirtualNode virtualNode, CancellationToken cancellationToken);
    }
}
