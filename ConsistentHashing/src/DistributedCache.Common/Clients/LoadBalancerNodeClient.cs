﻿using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;
using System.Net;

namespace DistributedCache.Common.Clients
{
    public class LoadBalancerNodeClient : ILoadBalancerNodeClient
    {
        private readonly ICustomHttpClient _httpClient;

        public LoadBalancerNodeClient(ICustomHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<LoadBalancerInformationModel> GetLoadBalancerInformationModelAsync(
            PhysicalNode loadBalancerPhysicalNode, 
            CancellationToken cancellationToken)
        {
            var url = $"{loadBalancerPhysicalNode.Location}load-balancer/values";
            var loadBalancerInformationModel = await _httpClient.GetAsync<LoadBalancerInformationModel>(new Uri(url), cancellationToken);

            return loadBalancerInformationModel;
        }

        public async Task AddVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode newVirtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken)
        {
            var url = $"{loadBalancerPhysicalNode.Location}load-balancer/node?physicalNodeUrl={WebUtility.UrlEncode(physicalNode.Location.ToString())}";
            await _httpClient.PostAsync(new Uri(url), newVirtualNode, cancellationToken);
        }

        public async Task RemoveVirtualNodeAsync(
            PhysicalNode loadBalancerPhysicalNode,
            VirtualNode virtualNode,
            PhysicalNode physicalNode,
            CancellationToken cancellationToken)
        {
            var url = $"{loadBalancerPhysicalNode.Location}load-balancer/node?physicalNodeUrl={WebUtility.UrlEncode(physicalNode.Location.ToString())}";
            await _httpClient.DeleteAsync(new Uri(url), virtualNode, cancellationToken);
        }
    }
}

