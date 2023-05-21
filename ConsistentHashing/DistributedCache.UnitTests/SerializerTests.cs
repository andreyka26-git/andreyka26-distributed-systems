using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;

namespace DistributedCache.UnitTests
{
    public class SerializerTests
    {
        private NewtonsoftSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _serializer = new NewtonsoftSerializer();
        }

        [Test]
        public void SerializeDeserialize_VirtualNode_ReturnsCorrect()
        {
            var virtualNode = new VirtualNode(6, 5);

            var serialized = _serializer.SerializeToJson(virtualNode);
            var deserialized = _serializer.Deserialize<VirtualNode>(serialized);

            Assert.AreEqual(5, deserialized.MaxItemsCount);
            Assert.AreEqual(6, deserialized.RingPosition);
        }

        [Test]
        public void SerializeDeserialize_PhysicalNode_ReturnsCorrect()
        {
            var node = new PhysicalNode(new Uri("https://localhost:123/route"));

            var serialized = _serializer.SerializeToJson(node);
            var deserialized = _serializer.Deserialize<PhysicalNode>(serialized);

            Assert.AreEqual(new Uri("https://localhost:123/route"), deserialized.Location);
        }

        [Test]
        public void SerializeDeserialize_ClusterInformationModel_ReturnsCorrect()
        {
            var physicalNode = new PhysicalNode(new Uri("https://localhost:123/route"));
            var node = new VirtualNode(6, 5);
            var cacheItems = new Dictionary<uint, string>
            {
                [1000] = "value1"
            };

            var childInfo = new ChildInformationModel()
            {
                VirtualNodesWithItems = new List<ChildInformationModelItem>
                {
                    new ChildInformationModelItem
                    {
                        Node = node,
                        CacheItems = cacheItems
                    }
                }
            };

            var loadBalancerInfo = new LoadBalancerInformationModel
            {
                ChildInformationModels = new List<LoadBalancerInformationModelItem>
                {
                    new LoadBalancerInformationModelItem
                    {
                        ChildInfo = childInfo,
                        PhysicalNode = physicalNode
                    }
                }
            };

            var clusterModel = new ClusterInformationModel
            {
                LoadBalancerInformations = new List<ClusterInformationModelItem>
                {
                    new ClusterInformationModelItem
                    {
                        LoadBalancerInfo = loadBalancerInfo,
                        PhysicalNode = physicalNode
                    }
                }
            };

            var serialized = _serializer.SerializeToJson(clusterModel);
            var deserialized = _serializer.Deserialize<ClusterInformationModel>(serialized);

            var deserializedLoadBalancerInfo = deserialized.LoadBalancerInformations.Single();
            var deserializedChildInfo = deserializedLoadBalancerInfo.LoadBalancerInfo.ChildInformationModels.Single();

            var deserializedVirtualNode = deserializedChildInfo.ChildInfo.VirtualNodesWithItems.Single();

            Assert.That(deserializedLoadBalancerInfo.PhysicalNode, Is.EqualTo(physicalNode));
            Assert.That(deserializedChildInfo.PhysicalNode, Is.EqualTo(physicalNode));
            Assert.That(deserializedVirtualNode.Node, Is.EqualTo(node));
            CollectionAssert.AreEquivalent(deserializedVirtualNode.CacheItems, cacheItems);
        }
    }
}
