using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public interface IMasterService
    {
        Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken);
    }
}
