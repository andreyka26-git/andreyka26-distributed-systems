using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public interface IRebalancingQueue
    {
        Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
