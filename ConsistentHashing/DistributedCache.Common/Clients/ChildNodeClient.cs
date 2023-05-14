using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.Clients
{
    public class ChildNodeClient : IChildNodeClient
    {
        private ICustomHttpClient _httpClient;

        public ChildNodeClient(ICustomHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task AddFirstHalfToNewNodeAsync(Dictionary<uint, string> cacheItems, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var url = $"{physicalNode.Location}/child-node/{virtualNode.RingPosition}/bulk";
            await _httpClient.PostAsync(new Uri(url), cacheItems, cancellationToken);
        }

        public async Task AddNewVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            var url = $"{physicalNode.Location}/child-node/nodes";
            await _httpClient.PostAsync(new Uri(url), virtualNode, cancellationToken);
        }

        public async Task AddToCacheAsync(AddToCacheModel addModel, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var url = $"{physicalNode.Location}/child-node/{addModel.VirtualNode.RingPosition}/{addModel.KeyHash}";
            await _httpClient.PostAsync(new Uri(url), addModel.Value, cancellationToken);
        }

        public async Task<int> GetCountOfItemsAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            var url = $"{physicalNode.Location}/child-node/{virtualNode.RingPosition}/count";
            await _httpClient.GetAsync<int>(new Uri(url), cancellationToken);
        }

        public async Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync(VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetFromCacheAsync(uint keyHash, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task RemoveFirstHalfOfCache(uint lastKeyHashInclusively, VirtualNode virtualNode, PhysicalNode physicalNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task RemoveVirtualNodeAsync(PhysicalNode physicalNode, VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
