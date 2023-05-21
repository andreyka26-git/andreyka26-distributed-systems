using DistributedCache.Common.Clients;
using DistributedCache.Common.Concurrency;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.InformationModels;
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
        private readonly IAsyncSerializableLockService _lockService;

        public MasterService(
            IChildNodeClient childClient,
            IChildNodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            ILoadBalancerNodeClient loadBalancerClient,
            IHashService hashService,
            IAsyncSerializableLockService lockService)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancerClient = loadBalancerClient;
            _hashService = hashService;
            _lockService = lockService;
        }

        public async Task<ClusterInformationModel> GetClusterInformationAsync(CancellationToken cancellationToken)
        {
            var clusterInformation = new ClusterInformationModel();

            foreach(var loadBalancer in _physicalNodeProvider.LoadBalancers)
            {
                var loadBalancerInformationModel = await _loadBalancerClient.GetLoadBalancerInformationModelAsync(loadBalancer, cancellationToken);
                clusterInformation.LoadBalancerInformations.Add(new ClusterInformationModelItem
                {
                    LoadBalancerInfo = loadBalancerInformationModel,
                    PhysicalNode = loadBalancer
                });
            }

            return clusterInformation;
        }

        public async Task<PhysicalNode> CreateLoadBalancerAsync(int port, CancellationToken cancellationToken)
        {
            var node = await _lockService.ExecuteSeriallyAsync(() =>
            {
                return CreateLoadBalancerNotSafeAsync(port, cancellationToken);
            });

            return node;
        }

        public async Task<PhysicalNode> CreateLoadBalancerNotSafeAsync(int port, CancellationToken cancellationToken)
        {
            var node = await _physicalNodeProvider.CreateLoadBalancerPhysicalNodeAsync(port, cancellationToken);
            return node;
        }

        public async Task<PhysicalNode> CreateNewChildNodeAsync(int port, CancellationToken cancellationToken)
        {
            var node = await _lockService.ExecuteSeriallyAsync(() =>
            {
                return CreateNewChildNodeNotSafeAsync(port, cancellationToken);
            });

            return node;
        }

        public async Task<PhysicalNode> CreateNewChildNodeNotSafeAsync(int port, CancellationToken cancellationToken)
        {
            var childNode = await _physicalNodeProvider.CreateChildPhysicalNodeAsync(port, cancellationToken);
            var virtualNode = new VirtualNode(_hashService.GetHash(childNode.Location.ToString()), MaxChildNodeItems);

            _nodeManager.AddPhysicalNode(childNode);
            _nodeManager.AddVirtualNode(virtualNode, childNode);

            await _childClient.AddNewVirtualNodeAsync(childNode, virtualNode, cancellationToken);

            foreach (var loadBalancer in _physicalNodeProvider.LoadBalancers)
            {
                await _loadBalancerClient.AddVirtualNodeAsync(loadBalancer, virtualNode, childNode, cancellationToken);
            }

            return childNode;
        }

        public async Task RebalanceNodeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _lockService.ExecuteSeriallyAsync(() =>
            {
                return RebalanceNodeNotSafeAsync(hotVirtualNode, cancellationToken);
            });
        }

        public async Task RebalanceNodeNotSafeAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);
            var newPhysicalNode = await _physicalNodeProvider.CreateChildPhysicalNodeAsync(cancellationToken: cancellationToken);

            var nodePosition = firstHalf.OrderBy(h => h.Key).Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition, hotVirtualNode.MaxItemsCount);

            foreach (var loadBalancerNode in _physicalNodeProvider.LoadBalancers)
            {
                await _loadBalancerClient.AddVirtualNodeAsync(loadBalancerNode, newVirtualNode, newPhysicalNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);

            // in case new item added while we are updating loadbalancers - we get first half again to include newly added and not loose data.
            firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(hotVirtualNode, hotPhysicalNode, cancellationToken);
        }
    }
}
