using DistributedCache.ChildNode;
using DistributedCache.ChildNode.Controllers;
using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;
using DistributedCache.LoadBalancer.Controllers;
using DistributedCache.Master.Controllers;
using DistributedCache.UnitTests.Fakes;
using Microsoft.AspNetCore.Identity;

namespace DistributedCache.UnitTests
{
    public class RebalancingFlowTests
    {
        private const int MaxItemsPerChild = 10;

        [SetUp]
        public void SetUp()
        {
            var rebalancingQueue = new RebalancingQueueFake();
            var childNodeClient = CreateChildNode(rebalancingQueue);


            var nodeManager = new NodeManager();
            var physicalProvider = new PhysicalNodeProviderFake();
            var master = new MasterController(childNodeClient, nodeManager, physicalProvider, );
        }

        private ILoadBalancerNodeClient CreateLoadBalancer()
        {
            var nodeManager = new NodeManager();
            var serializer = new NewtonsoftSerializer();
            var hashService = new JenkinsHashService(serializer);
            var hashingRing = new HashingRing();
            
            var loadBalancer = new LoadBalancerController(nodeManager, hashService, hashingRing);
        }

        private IChildNodeClient CreateChildNode(IRebalancingQueue queue)
        {
            var nodeManager = new VirtualNodeManager();
            var childNodeController = new ChildNodeController(nodeManager, queue);

            var childClient = new ChildNodeClientFake(childNodeController);

            return childClient;
        }
    }
}
