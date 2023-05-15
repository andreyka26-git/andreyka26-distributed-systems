using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public interface IMasterService
    {
        Task<PhysicalNode> CreateLoadBalancerAsync(int port, CancellationToken cancellationToken);
        Task<PhysicalNode> CreateNewChildNodeAsync(int port, CancellationToken cancellationToken);
        Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
