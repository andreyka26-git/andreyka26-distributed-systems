using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    public record AddToCacheDto(VirtualNode Node, uint KeyHash, string Value);
}
