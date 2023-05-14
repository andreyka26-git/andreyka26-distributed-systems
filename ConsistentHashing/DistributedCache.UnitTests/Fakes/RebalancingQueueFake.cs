using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Master.Controllers;

namespace DistributedCache.UnitTests.Fakes
{
    public class RebalancingQueueFake : IRebalancingQueue
    {
        public MasterController MasterController { get; set; }

        public async Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await MasterController.RebalanceNodeAsync(hotVirtualNode, cancellationToken);
        }
    }
}
