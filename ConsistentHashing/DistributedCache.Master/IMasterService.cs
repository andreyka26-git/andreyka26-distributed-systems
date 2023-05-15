using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public interface IMasterService
    {
        Task<ClusterInformationModel> GetClusterInformationAsync(CancellationToken cancellationToken);
        Task<PhysicalNode> CreateLoadBalancerAsync(int port, CancellationToken cancellationToken);
        Task<PhysicalNode> CreateNewChildNodeAsync(int port, CancellationToken cancellationToken);
        Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
