using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class LoadBalancerNodeClientFake : ILoadBalancerNodeClient
    {
        private readonly IHashingRing _hashingRing;

        public LoadBalancerNodeClientFake(
            IHashingRing hashingRing)
        {
            _hashingRing = hashingRing;
        }

        public Task AddVirtualNodeAsync(VirtualNode newVirtualNode, CancellationToken cancellationToken)
        {
            _hashingRing.AddVirtualNode(newVirtualNode);
            return Task.CompletedTask;
        }

        public Task RemoveVirtualNodeAsync(VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            _hashingRing.RemoveVirtualNode(virtualNode.RingPosition);
            return Task.CompletedTask;
        }
    }
}
