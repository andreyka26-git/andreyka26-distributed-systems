using DistributedCache.Common;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.LoadBalancer
{
    public class ChildNodeServiceFake : IChildNodeService
    {
        private readonly Dictionary<VirtualNode, Dictionary<uint, string>> _nodeToCacheMapping = new Dictionary<VirtualNode, Dictionary<uint, string>>();

        public Task AddToCacheAsync(AddToCacheDto addDto, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var cache = _nodeToCacheMapping[addDto.Node];
            cache[addDto.KeyHash] = addDto.Value;

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
