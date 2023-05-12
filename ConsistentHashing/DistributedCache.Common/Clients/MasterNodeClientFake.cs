using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class MasterNodeClientFake : IMasterNodeClient
    {
        private readonly IChildNodeClient _childClient;
        private readonly INodeManager _nodeManager;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;
        private readonly List<ILoadBalancerNodeClient> _loadBalancers;

        public MasterNodeClientFake(
            IChildNodeClient childClient,
            INodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            List<ILoadBalancerNodeClient> loadBalancers)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancers = loadBalancers;
        }

        // TODO make it serializable
        public async Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var newPhysicalNode = _physicalNodeProvider.CreateNewPhysicalNode();
            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);

            var nodePosition = firstHalf.Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition);

            foreach (var loadBalacer in _loadBalancers)
            {
                await loadBalacer.AddVirtualNodeAsync(newVirtualNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(nodePosition, hotVirtualNode, hotPhysicalNode, cancellationToken);
        }
    }
}
