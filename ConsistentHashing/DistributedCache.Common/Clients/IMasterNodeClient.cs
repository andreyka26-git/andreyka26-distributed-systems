using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface IMasterNodeClient
    {
        Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
