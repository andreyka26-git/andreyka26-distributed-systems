using DistributedCache.ChildNode;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests.Fakes
{
    internal class PhysicalNodeProviderFake : IPhysicalNodeProvider
    {
        private readonly List<PhysicalNode> _loadBalancers = new List<PhysicalNode>();
        private readonly List<PhysicalNode> _childNodes = new List<PhysicalNode>();

        public EventHandler<PhysicalNode> ChildCreated;
        public EventHandler<PhysicalNode> LoadBalancerCreated;

        public IReadOnlyList<PhysicalNode> LoadBalancers => _loadBalancers;
        public IReadOnlyList<PhysicalNode> ChildNodes => _childNodes;

        public Task<PhysicalNode> CreateChildPhysicalNodeAsync(int? port = null, CancellationToken cancellationToken = default)
        {
            var node = new PhysicalNode(new Uri($"http://some:{port}"));
            _childNodes.Add(node);

            ChildCreated?.Invoke(this, node);

            return Task.FromResult(node);
        }

        public Task<PhysicalNode> CreateLoadBalancerPhysicalNodeAsync(int? port = null, CancellationToken cancellationToken = default)
        {
            var node = new PhysicalNode(new Uri($"http://some:{port}"));
            _loadBalancers.Add(node);

            LoadBalancerCreated?.Invoke(this, node);

            return Task.FromResult(node);
        }
    }
}
