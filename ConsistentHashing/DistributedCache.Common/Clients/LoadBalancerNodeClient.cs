using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class LoadBalancerNodeClient : ILoadBalancerNodeClient
    {
        private readonly ICustomHttpClient _httpClient;


        public LoadBalancerNodeClient(ICustomHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task AddVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode newVirtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken)
        {
            var url = $"{loadBalancerPhysicalNode.Location}load-balancer/node/{physicalNode.Location}";
            await _httpClient.PostAsync(new Uri(url), newVirtualNode, cancellationToken);
        }

        public async Task RemoveVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode virtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken)
        {
            var url = $"{loadBalancerPhysicalNode.Location}load-balancer/node/{physicalNode.Location}";
            await _httpClient.DeleteAsync(new Uri(url), virtualNode, cancellationToken);
        }
    }
}

