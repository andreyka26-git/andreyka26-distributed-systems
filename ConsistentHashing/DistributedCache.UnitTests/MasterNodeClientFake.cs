using DistributedCache.Common.NodeManagement;
using DistributedCache.Master.Controllers;

namespace DistributedCache.Common.Clients
{
    public class MasterNodeClientFake : IMasterNodeClient
    {
        private readonly MasterController _masterController;

        public MasterNodeClientFake(MasterController masterController)
        {
            _masterController = masterController;
        }

        public async Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _masterController.RebalanceNodeAsync(hotVirtualNode, cancellationToken);
        }
    }
}
