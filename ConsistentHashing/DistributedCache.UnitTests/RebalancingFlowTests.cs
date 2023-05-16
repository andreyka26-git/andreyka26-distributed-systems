using DistributedCache.ChildNode;
using DistributedCache.Common;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;
using DistributedCache.LoadBalancer;
using DistributedCache.Master;
using DistributedCache.UnitTests.Fakes;

namespace DistributedCache.UnitTests
{
    public class RebalancingFlowTests
    {
        private MasterService _masterService;
        private PhysicalNodeProviderFake _physicalNodeProviderFake;

        private LoadBalancerClientFake _loadBalancerClientFake;
        private ChildClientFake _childClientFake;
        private JenkinsHashService _hashService;

        [SetUp]
        public async Task SetUp()
        {
            _childClientFake = new ChildClientFake();
            _loadBalancerClientFake = new LoadBalancerClientFake();

            var serializer = new NewtonsoftSerializer();
            _hashService = new JenkinsHashService(serializer);
            var hashingRing = new HashingRing(_hashService);

            var childNodeManager = new ChildNodeManager(hashingRing);
            _physicalNodeProviderFake = new PhysicalNodeProviderFake();

            _masterService = new MasterService(
                _childClientFake,
                childNodeManager,
                _physicalNodeProviderFake,
                _loadBalancerClientFake,
                _hashService);

            _physicalNodeProviderFake.ChildCreated += (obj, node) =>
            {
                var (queue, child) = GetChildNodeService();

                queue.OnNodeRebalancing += async (arg, virtualNode) =>
                {
                    await _masterService.RebalanceNodeAsync(virtualNode, CancellationToken.None);
                };

                _childClientFake.ChildNodeToServiceMapping.Add(node, child);
            };

            _physicalNodeProviderFake.LoadBalancerCreated += (obj, node) =>
            {
                var loadBalancer = GetLoadBalancerService(_childClientFake);

                _loadBalancerClientFake.LoadBalancerToServiceMapping.Add(node, loadBalancer);
            };
        }

        [Test]
        public async Task Add2LoadBalancers2Nodes_SuccessfullyCreated()
        {
            await _masterService.CreateLoadBalancerAsync(1000, CancellationToken.None);
            await _masterService.CreateNewChildNodeAsync(1001, CancellationToken.None);

            var firstLoadBalancer = _physicalNodeProviderFake.LoadBalancers[0];
            await _loadBalancerClientFake.LoadBalancerToServiceMapping[firstLoadBalancer].AddValueAsync("key1", "key1", CancellationToken.None);
            await _loadBalancerClientFake.LoadBalancerToServiceMapping[firstLoadBalancer].AddValueAsync("key2", "key2", CancellationToken.None);
            await _loadBalancerClientFake.LoadBalancerToServiceMapping[firstLoadBalancer].AddValueAsync("key3", "key3", CancellationToken.None);
            await _loadBalancerClientFake.LoadBalancerToServiceMapping[firstLoadBalancer].AddValueAsync("key4", "key4", CancellationToken.None);

            var firstChildPhysicalNode = _physicalNodeProviderFake.ChildNodes[0];
            var firstChildService = _childClientFake.ChildNodeToServiceMapping[firstChildPhysicalNode];
            var (node, cache) = firstChildService.NodeToCacheMapping.Single().Value;

            Assert.That(cache.Cache[_hashService.GetHash("key1")], Is.EqualTo("key1"));
            Assert.That(cache.Cache[_hashService.GetHash("key2")], Is.EqualTo("key2"));
            Assert.That(cache.Cache[_hashService.GetHash("key3")], Is.EqualTo("key3"));
            Assert.That(cache.Cache[_hashService.GetHash("key4")], Is.EqualTo("key4"));
            Assert.That(cache.Cache.Count, Is.EqualTo(4));
        }

        private LoadBalancerService GetLoadBalancerService(ChildClientFake childClient)
        {
            var serializer = new NewtonsoftSerializer();
            var hashingRing = new HashingRing(_hashService);

            var childNodeManager = new ChildNodeManager(hashingRing);

            var loadBalancer = new LoadBalancerService(childNodeManager, _hashService, childClient);

            return loadBalancer;
        }

        private (RebalancingQueueFake, ChildNodeService) GetChildNodeService()
        {
            var queueFake = new RebalancingQueueFake();
            var childService = new ChildNodeService(queueFake);

            return (queueFake, childService);
        }
    }
}
