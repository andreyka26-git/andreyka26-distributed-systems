using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public class MasterService : IMasterService
    {
        private readonly IChildNodeClient _childClient;
        private readonly INodeManager _nodeManager;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;
        private readonly ILoadBalancerNodeClient _loadBalancer;
        private readonly List<PhysicalNode> _loadBalancerNodes = new List<PhysicalNode>();

        public MasterService(
            IChildNodeClient childClient,
            INodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            ILoadBalancerNodeClient loadBalancer)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancer = loadBalancer;
        }

        public async Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var newPhysicalNode = _physicalNodeProvider.CreateNewPhysicalNode();
            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);

            var nodePosition = firstHalf.Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition, hotVirtualNode.MaxItemsCount);

            foreach (var loadBalancerNode in _loadBalancerNodes)
            {
                await _loadBalancer.AddVirtualNodeAsync(newVirtualNode, newPhysicalNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(nodePosition, hotVirtualNode, hotPhysicalNode, cancellationToken);
        }
    }
}
