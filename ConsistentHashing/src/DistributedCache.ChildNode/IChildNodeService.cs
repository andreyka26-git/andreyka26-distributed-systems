﻿using DistributedCache.Common.Cache;
using DistributedCache.Common.InformationModels;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.ChildNode
{
    public interface IChildNodeService : IDisposable
    {
        IReadOnlyDictionary<uint, IChildNodeInMemoryCache> NodeToCacheMapping { get; }
        Task<ChildInformationModel> GetChildClusterInformationModelAsync(CancellationToken cancellationToken);
        Task AddNodeAsync(VirtualNode node, CancellationToken cancellationToken);
        Task RemoveNodeAsync(uint position, CancellationToken cancellationToken);
        Task<string> GetValueAsync(uint nodePosition, uint keyHash, CancellationToken cancellationToken);
        Task<bool> AddValueAsync(uint nodePosition, uint KeyHash, string value, CancellationToken cancellationToken);
        Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(uint nodePosition, CancellationToken cancellationToken);

        Task RemoveFirstHalfOfCacheAsync(uint nodePosition, uint lastItemToRemoveInclusively, CancellationToken cancellationToken);
        Task AddBulkToCacheAsync(uint nodePosition, Dictionary<uint, string> cacheItems, CancellationToken cancellationToken);
        Task<int> GetCountAsync(uint nodePosition, CancellationToken cancellationToken);
    }
}
