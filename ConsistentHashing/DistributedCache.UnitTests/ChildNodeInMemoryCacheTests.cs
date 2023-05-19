using DistributedCache.Common;
using DistributedCache.Common.Concurrency;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests
{
    public class ChildNodeInMemoryCacheTests
    {
        [Test]
        public void GetFirstHalf_EvenAndItemsInTail_GetsFromStartAndTail()
        {
            //[{10}, 12, 13, 1, 2, 3]
            FirstHalfAssert(10, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            });

            //[10, {12}, 13, 1, 2, 3]
            FirstHalfAssert(12, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            });

            //[10, 12, {13}, 1, 2, 3]
            FirstHalfAssert(13, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
            });

            //[10, 12, 13, {1}, 2, 3]
            FirstHalfAssert(1, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [12] = "12",
                [13] = "13",
            });

            //[10, 12, 13, 1, {2}, 3]
            FirstHalfAssert(2, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [2] = "2",
                [1] = "1",
                [13] = "13",
            });

            //[10, 12, 13, 1, 2, {3}]
            FirstHalfAssert(3, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
                [13] = "13",
            },
            new Dictionary<uint, string>
            {
                [3] = "3",
                [2] = "2",
                [1] = "1",
            });
        }

        private void FirstHalfAssert(uint nodePos, Dictionary<uint, string> cacheItems, Dictionary<uint, string> expected)
        {
            var cache = new ThreadSafeChildNodeInMemoryCache(new VirtualNode(nodePos, 10), new ReadWriteLockService());
            cache.AddBulkToCache(cacheItems);

            CollectionAssert.AreEquivalent(expected, cache.GetFirstHalfOfCache());
        }

        [Test]
        public void GetFirstHalf_OddAndItemsInTail_GetsFromStartAndTail()
        {
            //[{10}, 12, 1, 2, 3]
            FirstHalfAssert(10, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            });

            //[10, {12}, 1, 2, 3]
            FirstHalfAssert(12, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            });

            //[10, 12, {1}, 2, 3]
            FirstHalfAssert(1, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [12] = "12",
            });

            //[10, 12, 1, {2}, 3]
            FirstHalfAssert(2, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            });


            //[10, 12, 1, 2, {3}]
            FirstHalfAssert(2, new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
                [3] = "3",
                [10] = "10",
                [12] = "12",
            },
            new Dictionary<uint, string>
            {
                [1] = "1",
                [2] = "2",
            });
        }
    }
}
