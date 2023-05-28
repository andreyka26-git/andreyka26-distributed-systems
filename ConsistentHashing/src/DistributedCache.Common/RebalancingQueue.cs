using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    public class RebalancingQueue : IRebalancingQueue
    {
        private const string MasterUrl = "https://localhost:7001";
        private readonly ICustomHttpClient _httpClient;

        public RebalancingQueue(
            ICustomHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _httpClient.PostAsync(new Uri($"{MasterUrl}/master/rebalance"), hotVirtualNode, cancellationToken);
        }
    }
}
