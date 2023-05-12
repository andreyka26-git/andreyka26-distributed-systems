using DistributedCache.Common.Cache;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.ChildNode
{
    public class VirtualNodeManager : IVirtualNodeManager
    {
        public Dictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> NodeToCacheMapping { get; } = new Dictionary<uint, (VirtualNode, IChildNodeInMemoryCache)>();
    }
}
