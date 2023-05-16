using DistributedCache.Common.Clients;
using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;
using DistributedCache.LoadBalancer;

namespace DistributedCache.UnitTests.Fakes
{
    internal class LoadBalancerClientFake : ILoadBalancerNodeClient
    {
        public Dictionary<PhysicalNode, LoadBalancerService> LoadBalancerToServiceMapping = new Dictionary<PhysicalNode, LoadBalancerService>();

        public LoadBalancerClientFake()
        {
        }

        public async Task AddVirtualNodeAsync(PhysicalNode loadBalancerPhysicalNode, VirtualNode newVirtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            await LoadBalancerToServiceMapping[loadBalancerPhysicalNode].AddVirtualNodeAsync(physicalNode.Location.ToString(), newVirtualNode, cancellationToken);
        }

        public Task<LoadBalancerInformationModel> GetLoadBalancerInformationModelAsync(PhysicalNode loadBalancerPhysicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task RemoveVirtualNodeAsync(PhysicalNode loadBalancerPhysicalNode, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
