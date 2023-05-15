using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using Microsoft.Extensions.Options;

namespace DistributedCache.Master
{
    public class MasterService : IMasterService
    {
        private readonly IChildNodeClient _childClient;
        private readonly INodeManager _nodeManager;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;
        private readonly ILoadBalancerNodeClient _loadBalancerClient;
        private readonly List<PhysicalNode> _loadBalancerNodes = new List<PhysicalNode>();

        public MasterService(
            IChildNodeClient childClient,
            INodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            ILoadBalancerNodeClient loadBalancerClient,
            IOptions<LoadBalancerOptions> options)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancerClient = loadBalancerClient;

            foreach (var loadBalancer in options.Value.LoadBalancerUrls)
            {
                var loadBalancerNode = new PhysicalNode(new Uri(loadBalancer));
            }
        }

        public async Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var newPhysicalNode = await _physicalNodeProvider.CreateNewPhysicalNodeAsync(cancellationToken);
            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);

            var nodePosition = firstHalf.Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition, hotVirtualNode.MaxItemsCount);

            foreach (var loadBalancerNode in _loadBalancerNodes)
            {
                await _loadBalancerClient.AddVirtualNodeAsync(loadBalancerNode, newVirtualNode, newPhysicalNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(nodePosition, hotVirtualNode, hotPhysicalNode, cancellationToken);
        }
    }
}
