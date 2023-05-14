using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using Microsoft.Extensions.Configuration;

namespace DistributedCache.Common
{
    public class RebalancingQueue : IRebalancingQueue
    {
        private readonly ICustomHttpClient _httpClient;
        private readonly string _masterUrl;

        public RebalancingQueue(
            IConfiguration configuration,
            ICustomHttpClient httpClient)
        {
            _httpClient = httpClient;
            _masterUrl = configuration.GetValue<string?>("MasterUrl");

            if (string.IsNullOrEmpty(_masterUrl))
            {
                throw new ArgumentException($"{nameof(_masterUrl)} is empty");
            }
        }

        public async Task EmitNodeRebalancingAsync(VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _httpClient.PostAsync(new Uri($"{_masterUrl}/master/rebalance"), hotVirtualNode, cancellationToken);
        }
    }
}
