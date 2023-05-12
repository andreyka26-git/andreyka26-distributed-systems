using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;
using DistributedCache.LoadBalancer.Controllers;
using DistributedCache.Master.Controllers;

namespace DistributedCache.UnitTests
{
    public class RebalancingFlowTests
    {
        private const int MaxItemsPerChild = 10;

        [SetUp]
        public void SetUp()
        {
            var nodeManager = new NodeManager();

            var serializer = new NewtonsoftSerializer();
            var hashService = new JenkinsHashService(serializer);
            var hashingRing = new HashingRing(hashService);

            var childNodeClient = new ChildNodeClientFake(MaxItemsPerChild);

            var master = new MasterController(nodeManager, hashService, hashingRing, childNodeClient);

            var loadBalancers = new List<LoadBalancerController>();
            loadBalancers.Add(new LoadBalancerController(nodeManager, hashService, hashingRing, childNodeClient));
        }
    }
}
