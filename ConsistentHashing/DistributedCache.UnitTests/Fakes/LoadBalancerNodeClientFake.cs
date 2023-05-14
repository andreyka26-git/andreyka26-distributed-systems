using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using DistributedCache.LoadBalancer.Controllers;

namespace DistributedCache.UnitTests.Fakes
{
    public class LoadBalancerNodeClientFake : ILoadBalancerNodeClient
    {
        private readonly LoadBalancerController _loadBalancerController;

        public LoadBalancerNodeClientFake(LoadBalancerController loadBalancerController)
        {
            _loadBalancerController = loadBalancerController;
        }

        public async Task AddVirtualNodeAsync(VirtualNode newVirtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await _loadBalancerController.AddVirtualNodeAsync(physicalNode.Location.ToString(), newVirtualNode, cancellationToken);
        }

        public async Task RemoveVirtualNodeAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await _loadBalancerController.RemoveVirtualNodeAsync(physicalNode.ToString(), virtualNode, cancellationToken);
        }
    }
}
