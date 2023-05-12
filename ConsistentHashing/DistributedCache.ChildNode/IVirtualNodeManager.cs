using DistributedCache.Common.Cache;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.ChildNode
{
    public interface IVirtualNodeManager
    {
        Dictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> NodeToCacheMapping { get; }
    }
}
