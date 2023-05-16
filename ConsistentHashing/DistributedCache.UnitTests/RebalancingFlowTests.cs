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

        [SetUp]
        public async Task SetUp()
        {
            _childClientFake = new ChildClientFake();
            _loadBalancerClientFake = new LoadBalancerClientFake();

            var serializer = new NewtonsoftSerializer();
            var hashService = new JenkinsHashService(serializer);
            var hashingRing = new HashingRing(hashService);

            var childNodeManager = new ChildNodeManager(hashingRing);
            _physicalNodeProviderFake = new PhysicalNodeProviderFake();

            _masterService = new MasterService(
                _childClientFake,
                childNodeManager,
                _physicalNodeProviderFake,
                _loadBalancerClientFake,
                hashService);

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
            await _loadBalancerClientFake.LoadBalancerToServiceMapping[firstLoadBalancer].AddValueAsync("key1", "key2", CancellationToken.None);
        }

        private LoadBalancerService GetLoadBalancerService(ChildClientFake childClient)
        {
            var serializer = new NewtonsoftSerializer();
            var hashService = new JenkinsHashService(serializer);
            var hashingRing = new HashingRing(hashService);

            var childNodeManager = new ChildNodeManager(hashingRing);

            var loadBalancer = new LoadBalancerService(childNodeManager, hashService, childClient);

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
