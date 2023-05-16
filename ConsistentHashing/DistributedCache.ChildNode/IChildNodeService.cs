using DistributedCache.Common.Cache;
using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.ChildNode
{
    public interface IChildNodeService
    {
        Task<ChildInformationModel> GetChildClusterInformationModelAsync(CancellationToken cancellationToken);
        IReadOnlyDictionary<uint, (VirtualNode Node, IChildNodeInMemoryCache Cache)> NodeToCacheMapping { get; }
        Task AddNodeAsync(VirtualNode node, CancellationToken cancellationToken);
        Task RemoveNodeAsync(uint position, CancellationToken cancellationToken);
        Task<string> GetValueAsync(uint nodePosition, uint keyHash, CancellationToken cancellationToken);
        Task<bool> AddValueAsync(uint nodePosition, uint KeyHash, string value, CancellationToken cancellationToken);
        Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken);

        Task RemoveFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken);
        Task AddBulkToCacheAsync(uint nodePosition, Dictionary<uint, string> cacheItems, CancellationToken cancellationToken);
        Task<int> GetCountAsync(uint nodePosition, CancellationToken cancellationToken);
    }
}
