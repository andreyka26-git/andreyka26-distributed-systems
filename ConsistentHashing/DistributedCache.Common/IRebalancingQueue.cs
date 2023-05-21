using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    public interface IRebalancingQueue
    {
        // TODO make async
        Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
