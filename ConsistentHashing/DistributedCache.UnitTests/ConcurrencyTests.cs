using DistributedCache.Common;
using DistributedCache.Common.Cache;
using DistributedCache.Common.Concurrency;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.UnitTests
{
    public class ConcurrencyTests
    {
        private ThreadSafeChildNodeInMemoryCache _cache;

        [SetUp]
        public void SetUp()
        {
            _cache = new ThreadSafeChildNodeInMemoryCache(new VirtualNode(5, 1000000), new ReadWriteLockService());
        }

        [Test]
        public void ConcurrentReads_CanReadAtTheSameTime()
        {
            _cache.AddToCache(5, "val");

            Task.Run(() =>
            {
                for (var i = 0; i < 1000000; i++)
                {
                    var val = _cache.GetFromCache(5);
                    Assert.That(val, Is.EqualTo("val"));
                }
            });

            Task.Run(() =>
            {
                for (var i = 0; i < 1000000; i++)
                {
                    var val = _cache.GetFromCache(5);
                    Assert.That(val, Is.EqualTo("val"));
                }
            });
        }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        [Test]
        public async Task ConcurrentReadWrite_SuccessfullyWritesAndReads()
        {
            for (uint i = 0; i < 100000; i++)
            {
                var isAdded = _cache.AddToCache(i, $"val{i}");
                Assert.IsFalse(isAdded);
            }

            // start reading parallelly
            Task.Run(async () =>
            {
                for (uint i = 0; i < 100000; i++)
                {
                    var val = _cache.GetFromCache(i);
                    Assert.That(val, Is.EqualTo($"val{i}"));
                }
            });

            // start writing parallelly
            var writeTask = Task.Run(() =>
            {
                for (uint i = 100001; i < 100100; i++)
                {
                    var needRebalance = _cache.AddToCache(i, $"val{i}");
                }
            });

            // start reading parallelly
            Task.Run(async () =>
            {
                for (uint i = 0; i < 100000; i++)
                {
                    var val = _cache.GetFromCache(i);
                    Assert.That(val, Is.EqualTo($"val{i}"));
                }
            });

            // await writing
            await writeTask;


            // check everything was written while writing
            for (uint i = 100001; i < 100100; i++)
            {
                var val = _cache.GetFromCache(i);
                Assert.That(val, Is.EqualTo($"val{i}"));
            }
        }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }
}
