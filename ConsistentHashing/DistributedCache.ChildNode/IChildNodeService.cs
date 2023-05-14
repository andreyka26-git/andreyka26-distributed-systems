using DistributedCache.Common.Cache;
using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.ChildNode
{
    public interface IChildNodeService
    {
        IReadOnlyDictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> NodeToCacheMapping { get; }
        Task AddNodeAsync([FromBody] VirtualNode node, CancellationToken cancellationToken);
        Task RemoveNodeAsync([FromRoute] uint position, CancellationToken cancellationToken);
        Task<string> GetValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, CancellationToken cancellationToken);
        Task<bool> AddValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, [FromBody] string value, CancellationToken cancellationToken);
        Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken);

        Task RemoveFirstHalfOfCacheAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken);
        Task AddBulkToCacheAsync([FromRoute] uint nodePosition, [FromBody] Dictionary<uint, string> cacheItems, CancellationToken cancellationToken);
        Task<int> GetCountAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken);
    }
}
