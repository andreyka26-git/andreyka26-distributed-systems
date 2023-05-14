using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common;

namespace DistributedCache.LoadBalancer
{
    public class LoadBalancerService : ILoadBalancerService
    {
        private readonly INodeManager _nodeManager;
        private readonly IHashService _hashService;
        private readonly IChildNodeClient _childNodeClient;

        public LoadBalancerService(
            INodeManager nodeManager,
            IHashService hashService,
            IChildNodeClient childNodeClient)
        {
            _nodeManager = nodeManager;
            _hashService = hashService;
            _childNodeClient = childNodeClient;
        }

        public Task AddVirtualNodeAsync(string physicalNodeUrl, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            var physicalNode = new PhysicalNode(new Uri(physicalNodeUrl));

            _nodeManager.AddVirtualNode(virtualNode, physicalNode);
            
            return Task.CompletedTask;
        }

        public Task RemoveVirtualNodeAsync(string physicalNodeUrl, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            var physicalNode = new PhysicalNode(new Uri(physicalNodeUrl));

            _nodeManager.RemoveVirtualNode(virtualNode, physicalNode);
            return Task.CompletedTask;
        }

        public async Task<string> GetValueAsync( string key, CancellationToken cancellationToken)
        {
            var hashKey = _hashService.GetHash(key);

            var virtualNode = _nodeManager.GetVirtualNodeForHash(hashKey);
            var physicalNode = _nodeManager.ResolvePhysicalNode(virtualNode);

            var value = await _childNodeClient.GetFromCacheAsync(hashKey, virtualNode, physicalNode, cancellationToken);
            return value;
        }

        public async Task AddValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            var hashKey = _hashService.GetHash(key);
            var virtualNode = _nodeManager.GetVirtualNodeForHash(hashKey);
            var physicalNode = _nodeManager.ResolvePhysicalNode(virtualNode);

            var addToCacheModel = new AddToCacheModel(virtualNode, hashKey, value);
            await _childNodeClient.AddToCacheAsync(addToCacheModel, physicalNode, cancellationToken);
        }
    }
}
