using DistributedCache.Common;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using Moq;

namespace DistributedCache.UnitTests
{
    public class HashingRingTests
    {
        private HashingRing _hashingRing;
        private Mock<IHashService> _hashingServiceMock;

        [SetUp]
        public void SetUp()
        {
            _hashingServiceMock = new Mock<IHashService>();
            _hashingServiceMock
                .Setup(h => h.MaxHashValue)
                .Returns(int.MaxValue);

            _hashingRing = new HashingRing(_hashingServiceMock.Object);
        }

        [Test]
        public void BinarySearch_WhenLessMaxRingValue_ReturnsCorrect()
        {
            var nodePositions = new List<uint> { 1, 6, 50, 60, 360};

            _hashingServiceMock
                .Setup(h => h.MaxHashValue)
                .Returns(360);

            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 1), Is.EqualTo(1));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 2), Is.EqualTo(6));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 40), Is.EqualTo(50));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 55), Is.EqualTo(60));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 360), Is.EqualTo(1));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 361), Is.EqualTo(1));
            Assert.That(_hashingRing.BinarySearchRightMostNode(nodePositions, 362), Is.EqualTo(6));
        }

        public static IEnumerable<TestCaseData> GetBinarySearchTestData()
        {
            yield return new TestCaseData(new List<uint> { (uint)1 }, (uint)2, (uint)1);
            yield return new TestCaseData(new List<uint> { (uint)1 }, (uint)5, (uint)1);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6 }, (uint)1, (uint)1);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6 }, (uint)2, (uint)6);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6 }, (uint)5, (uint)6);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6 }, (uint)7, (uint)1);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6, 7 }, (uint)100, (uint)1);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6, 7 }, (uint)7, (uint)7);
            yield return new TestCaseData(new List<uint> { (uint)1, (uint)6, 7 }, (uint)6, (uint)6);
        }

        [TestCaseSource(nameof(GetBinarySearchTestData))]
        public void BinarySearch_WhenHaveNodes_ReturnsCorrect(IList<uint> nodePositions, uint keyPosition, uint expectedNodePosition)
        {
            var nodePosition = _hashingRing.BinarySearchRightMostNode(nodePositions, keyPosition);

            Assert.That(nodePosition, Is.EqualTo(expectedNodePosition));
        }

        [TestCaseSource(nameof(GetBinarySearchTestData))]
        public void GetVirtualNodeForHash_WhenHaveNodes_ReturnsCorrect(IList<uint> nodePositions, uint keyPosition, uint expectedNodePosition)
        {
            foreach(var nodePos in nodePositions)
            {
                _hashingRing.AddVirtualNode(new VirtualNode(nodePos, 10));
            }

            var nodePosition = _hashingRing.GetVirtualNodeForHash(keyPosition);

            Assert.That(nodePosition.RingPosition, Is.EqualTo(expectedNodePosition));
        }
    }
}
