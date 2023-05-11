using DistributedCache.Common;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.LoadBalancer
{
    public class ChildNodeServiceFake : IChildNodeService
    {
        private readonly Dictionary<VirtualNode, Dictionary<uint, string>> _nodeToCacheMapping = new Dictionary<VirtualNode, Dictionary<uint, string>>();

        public Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[addModel.VirtualNode];
            cache[addModel.KeyHash] = addModel.Value;

            return Task.CompletedTask;
        }

        public Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[virtualNode];

            if (!cache.TryGetValue(keyHash, out var value))
            {
                throw new Exception("Not Found");
            }

            return Task.FromResult(value);
        }
    }
}
