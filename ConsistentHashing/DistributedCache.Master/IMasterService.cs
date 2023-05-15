using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public interface IMasterService
    {
        Task CreateLoadBalancerAsync(int port, CancellationToken cancellationToken);
        Task CreateNewChildNodeAsync(int port, CancellationToken cancellationToken);
        Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
