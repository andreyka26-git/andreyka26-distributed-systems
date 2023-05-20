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
        private readonly CancellationToken _defaultCancellationToken = CancellationToken.None;

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
                    await _masterService.RebalanceNodeAsync(virtualNode, _defaultCancellationToken);
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
        public async Task MvpSetUp_Add4Items_CanGetThemBack()
        {
            await _masterService.CreateLoadBalancerAsync(1000, _defaultCancellationToken);
            await _masterService.CreateNewChildNodeAsync(1001, _defaultCancellationToken);

            var loadBalancerNode = _physicalNodeProviderFake.LoadBalancers[0];
            var loadBalancerService = _loadBalancerClientFake.LoadBalancerToServiceMapping[loadBalancerNode];

            await loadBalancerService.AddValueAsync("key1", "key1", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key2", "key2", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key3", "key3", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key4", "key4", _defaultCancellationToken);

            var firstChildPhysicalNode = _physicalNodeProviderFake.ChildNodes[0];
            var firstChildService = _childClientFake.ChildNodeToServiceMapping[firstChildPhysicalNode];
            var (node, cache) = firstChildService.NodeToCacheMapping.Single();

            Assert.That(cache.Cache[_hashService.GetHash("key1")], Is.EqualTo("key1"));
            Assert.That(cache.Cache[_hashService.GetHash("key2")], Is.EqualTo("key2"));
            Assert.That(cache.Cache[_hashService.GetHash("key3")], Is.EqualTo("key3"));
            Assert.That(cache.Cache[_hashService.GetHash("key4")], Is.EqualTo("key4"));
            Assert.That(cache.Cache.Count, Is.EqualTo(4));

            Assert.That(await loadBalancerService.GetValueAsync("key1", _defaultCancellationToken), Is.EqualTo("key1"));
            Assert.That(await loadBalancerService.GetValueAsync("key2", _defaultCancellationToken), Is.EqualTo("key2"));
            Assert.That(await loadBalancerService.GetValueAsync("key3", _defaultCancellationToken), Is.EqualTo("key3"));
            Assert.That(await loadBalancerService.GetValueAsync("key4", _defaultCancellationToken), Is.EqualTo("key4"));
        }

        [Test]
        public async Task MvpSetUp_RebalanceTo2HotNode_CanGetThemBack()
        {
            await _masterService.CreateLoadBalancerAsync(1000, _defaultCancellationToken);
            await _masterService.CreateNewChildNodeAsync(1001, _defaultCancellationToken);

            var loadBalancerNode = _physicalNodeProviderFake.LoadBalancers[0];
            var loadBalancerService = _loadBalancerClientFake.LoadBalancerToServiceMapping[loadBalancerNode];

            await loadBalancerService.AddValueAsync("key1", "key1", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key2", "key2", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key3", "key3", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key4", "key4", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key5", "key5", _defaultCancellationToken);

            var childPhysicalNode1 = _physicalNodeProviderFake.ChildNodes[0];
            var childService1 = _childClientFake.ChildNodeToServiceMapping[childPhysicalNode1];
            var (node1, cache1) = childService1.NodeToCacheMapping.Single();

            var childPhysicalNode2 = _physicalNodeProviderFake.ChildNodes[1];
            var childService2 = _childClientFake.ChildNodeToServiceMapping[childPhysicalNode2];
            var (node2, cache2) = childService2.NodeToCacheMapping.Single();

            var allInfo = await _masterService.GetClusterInformationAsync(_defaultCancellationToken);
            var firstLoadBalancer = allInfo.LoadBalancerInformations.First().Value;
            var allVirtualNodes = firstLoadBalancer.ChildInformationModels.SelectMany(c => c.Value.VirtualNodesWithItems).ToList();

            var allCount = allVirtualNodes.Sum(c => c.Value.Count);

            Assert.That(cache1.Cache.Count, Is.LessThanOrEqualTo(allCount / 2 + 1));
            Assert.That(cache2.Cache.Count, Is.LessThanOrEqualTo(allCount / 2 + 1));

            Assert.That(await loadBalancerService.GetValueAsync("key1", _defaultCancellationToken), Is.EqualTo("key1"));
            Assert.That(await loadBalancerService.GetValueAsync("key2", _defaultCancellationToken), Is.EqualTo("key2"));
            Assert.That(await loadBalancerService.GetValueAsync("key3", _defaultCancellationToken), Is.EqualTo("key3"));
            Assert.That(await loadBalancerService.GetValueAsync("key4", _defaultCancellationToken), Is.EqualTo("key4"));
            Assert.That(await loadBalancerService.GetValueAsync("key5", _defaultCancellationToken), Is.EqualTo("key5"));
        }

        [Test]
        public async Task MvpSetUp_RebalanceTo3HotNode_CanGetThemBack()
        {
            await _masterService.CreateLoadBalancerAsync(1000, _defaultCancellationToken);
            await _masterService.CreateNewChildNodeAsync(1001, _defaultCancellationToken);

            var loadBalancerNode = _physicalNodeProviderFake.LoadBalancers[0];
            var loadBalancerService = _loadBalancerClientFake.LoadBalancerToServiceMapping[loadBalancerNode];

            await loadBalancerService.AddValueAsync("key1", "key1", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key2", "key2", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key3", "key3", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key4", "key4", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key5", "key5", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key6", "key6", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key7", "key7", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key8", "key8", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key9", "key9", _defaultCancellationToken);
            await loadBalancerService.AddValueAsync("key10", "key10", _defaultCancellationToken);

            var childPhysicalNode1 = _physicalNodeProviderFake.ChildNodes[0];
            var childService1 = _childClientFake.ChildNodeToServiceMapping[childPhysicalNode1];
            var (node1, cache1) = childService1.NodeToCacheMapping.Single();

            var childPhysicalNode2 = _physicalNodeProviderFake.ChildNodes[1];
            var childService2 = _childClientFake.ChildNodeToServiceMapping[childPhysicalNode2];
            var (node2, cache2) = childService2.NodeToCacheMapping.Single();

            var childPhysicalNode3 = _physicalNodeProviderFake.ChildNodes[1];
            var childService3 = _childClientFake.ChildNodeToServiceMapping[childPhysicalNode3];
            var (node3, cache3) = childService3.NodeToCacheMapping.Single();

            var allInfo = await _masterService.GetClusterInformationAsync(_defaultCancellationToken);
            var firstLoadBalancer = allInfo.LoadBalancerInformations.First().Value;
            var allVirtualNodes = firstLoadBalancer.ChildInformationModels
                .SelectMany(c => c.Value.VirtualNodesWithItems)
                .OrderBy(c => c.Value.Max(v => v.Key))
                .ToList();

            var allCount = allVirtualNodes.Sum(c => c.Value.Count);

            Assert.That(cache1.Cache.Count, Is.LessThanOrEqualTo(allCount / 2 + 1));
            Assert.That(cache2.Cache.Count, Is.LessThanOrEqualTo(allCount / 2 + 1));
            Assert.That(cache3.Cache.Count, Is.LessThanOrEqualTo(allCount / 2 + 1));

            Assert.That(await loadBalancerService.GetValueAsync("key1", _defaultCancellationToken), Is.EqualTo("key1"));
            Assert.That(await loadBalancerService.GetValueAsync("key2", _defaultCancellationToken), Is.EqualTo("key2"));
            Assert.That(await loadBalancerService.GetValueAsync("key3", _defaultCancellationToken), Is.EqualTo("key3"));
            Assert.That(await loadBalancerService.GetValueAsync("key4", _defaultCancellationToken), Is.EqualTo("key4"));
            Assert.That(await loadBalancerService.GetValueAsync("key5", _defaultCancellationToken), Is.EqualTo("key5"));
            Assert.That(await loadBalancerService.GetValueAsync("key6", _defaultCancellationToken), Is.EqualTo("key6"));
            Assert.That(await loadBalancerService.GetValueAsync("key7", _defaultCancellationToken), Is.EqualTo("key7"));
            Assert.That(await loadBalancerService.GetValueAsync("key8", _defaultCancellationToken), Is.EqualTo("key8"));
            Assert.That(await loadBalancerService.GetValueAsync("key9", _defaultCancellationToken), Is.EqualTo("key9"));
        }

        private LoadBalancerService GetLoadBalancerService(ChildClientFake childClient)
        {
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
