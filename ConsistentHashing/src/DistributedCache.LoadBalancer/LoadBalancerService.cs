﻿using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common;
using DistributedCache.Common.InformationModels;

namespace DistributedCache.LoadBalancer
{
    public class LoadBalancerService : ILoadBalancerService
    {
        private readonly IChildNodeManager _nodeManager;
        private readonly IHashService _hashService;
        private readonly IChildNodeClient _childNodeClient;

        public LoadBalancerService(
            IChildNodeManager nodeManager,
            IHashService hashService,
            IChildNodeClient childNodeClient)
        {
            _nodeManager = nodeManager;
            _hashService = hashService;
            _childNodeClient = childNodeClient;
        }

        public async Task<LoadBalancerInformationModel> GetLoadBalancerInformationAsync(CancellationToken cancellationToken)
        {
            var model = new LoadBalancerInformationModel();

            foreach (var (node, virtualNodes) in _nodeManager.PhysicalToVirtualMapping)
            {
                var childModel = await _childNodeClient.GetChildClusterInformationModelAsync(node, cancellationToken);
                model.ChildInformationModels.Add(
                    new LoadBalancerInformationModelItem
                    {
                        PhysicalNode = node,
                        ChildInfo = childModel
                    });
            }

            return model;
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

        public async Task<string> GetValueAsync(string key, CancellationToken cancellationToken)
        {
            var keyHash = _hashService.GetHash(key);

            var virtualNode = _nodeManager.GetVirtualNodeForHash(keyHash);
            var physicalNode = _nodeManager.ResolvePhysicalNode(virtualNode);

            var value = await _childNodeClient.GetFromCacheAsync(keyHash, virtualNode, physicalNode, cancellationToken);
            return value;
        }

        public async Task AddValueAsync(string key, string value, CancellationToken cancellationToken)
        {
            var keyHash = _hashService.GetHash(key);
            var virtualNode = _nodeManager.GetVirtualNodeForHash(keyHash);
            var physicalNode = _nodeManager.ResolvePhysicalNode(virtualNode);

            var addToCacheModel = new AddToCacheModel(virtualNode, keyHash, value);
            await _childNodeClient.AddToCacheAsync(addToCacheModel, physicalNode, cancellationToken);
        }
    }
}
