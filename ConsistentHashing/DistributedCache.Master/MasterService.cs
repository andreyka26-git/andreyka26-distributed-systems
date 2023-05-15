using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Master
{
    public class MasterService : IMasterService
    {
        private const int MaxChildNodeItems = 5;

        private readonly IChildNodeClient _childClient;
        private readonly IChildNodeManager _nodeManager;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;
        private readonly ILoadBalancerNodeClient _loadBalancerClient;
        private readonly IHashService _hashService;

        public MasterService(
            IChildNodeClient childClient,
            IChildNodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            ILoadBalancerNodeClient loadBalancerClient,
            IHashService hashService)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancerClient = loadBalancerClient;
            _hashService = hashService;
        }

        public async Task CreateLoadBalancerAsync(int port, CancellationToken cancellationToken)
        {
            await _physicalNodeProvider.CreateLoadBalancerPhysicalNodeAsync(port, cancellationToken);
        }

        public async Task CreateNewChildNodeAsync(int port, CancellationToken cancellationToken)
        {
            var childNode = await _physicalNodeProvider.CreateChildPhysicalNodeAsync(port, cancellationToken);
            var virtualNode = new VirtualNode(_hashService.GetHash(childNode.Location.ToString()), MaxChildNodeItems);

            _nodeManager.AddPhysicalNode(childNode);
            _nodeManager.AddVirtualNode(virtualNode, childNode);

            await _childClient.AddNewVirtualNodeAsync(childNode, virtualNode, cancellationToken);

            foreach(var loadBalancer in _physicalNodeProvider.LoadBalancers)
            {
                await _loadBalancerClient.AddVirtualNodeAsync(loadBalancer, virtualNode, childNode, cancellationToken);
            }
        }

        public async Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var newPhysicalNode = await _physicalNodeProvider.CreateChildPhysicalNodeAsync(cancellationToken: cancellationToken);
            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);

            var nodePosition = firstHalf.Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition, hotVirtualNode.MaxItemsCount);

            foreach (var loadBalancerNode in _physicalNodeProvider.LoadBalancers)
            {
                await _loadBalancerClient.AddVirtualNodeAsync(loadBalancerNode, newVirtualNode, newPhysicalNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(nodePosition, hotVirtualNode, hotPhysicalNode, cancellationToken);
        }
    }
}
