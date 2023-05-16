using DistributedCache.Common;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests.Fakes
{
    internal class RebalancingQueueFake : IRebalancingQueue
    {
        public EventHandler<VirtualNode> OnNodeRebalancing;

        public Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
