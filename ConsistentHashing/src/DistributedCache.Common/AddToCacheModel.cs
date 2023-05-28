using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    public record AddToCacheModel(VirtualNode VirtualNode, uint KeyHash, string Value);
}
