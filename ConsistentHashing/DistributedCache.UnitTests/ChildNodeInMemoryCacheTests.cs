using DistributedCache.Common;

namespace DistributedCache.UnitTests
{
    public class ChildNodeInMemoryCacheTests
    {
        [Test]
        public void GetFirstHalf_EvenAndItemsInTail_GetsFromStartAndTail()
        {
            var cache = new ChildNodeInMemoryCache(10);

            //[10, 12, 13, 1, 2, 3]
            cache.AddBulkToCache(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            });

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            }, cache.GetFirstHalfOfCache(10));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            }, cache.GetFirstHalfOfCache(12));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            }, cache.GetFirstHalfOfCache(13));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [12] = "12",
                [13] = "13",
            }, cache.GetFirstHalfOfCache(1));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [2] = "2",
                [1] = "1",
                [13] = "13",
            }, cache.GetFirstHalfOfCache(2));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [3] = "3",
                [2] = "2",
                [1] = "1",
            }, cache.GetFirstHalfOfCache(3));
        }

        [Test]
        public void GetFirstHalf_OddAndItemsInTail_GetsFromStartAndTail()
        {
            var cache = new ChildNodeInMemoryCache(10);

            //[10, 12, 13, 1, 2, 3]
            cache.AddBulkToCache(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            });

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            }, cache.GetFirstHalfOfCache(10));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            }, cache.GetFirstHalfOfCache(12));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [12] = "12",
            }, cache.GetFirstHalfOfCache(1));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            }, cache.GetFirstHalfOfCache(2));

            CollectionAssert.AreEquivalent(new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            }, cache.GetFirstHalfOfCache(3));
        }
    }
}
