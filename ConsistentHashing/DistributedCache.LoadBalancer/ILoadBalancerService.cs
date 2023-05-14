using DistributedCache.Common.NodeManagement;

namespace DistributedCache.LoadBalancer
{
    public interface ILoadBalancerService
    {
        Task AddVirtualNodeAsync(string physicalNodeUrl, VirtualNode virtualNode, CancellationToken cancellationToken);
        Task RemoveVirtualNodeAsync(string physicalNodeUrl, VirtualNode virtualNode, CancellationToken cancellationToken);
        Task<string> GetValueAsync(string key, CancellationToken cancellationToken);
        Task AddValueAsync(string key, string value, CancellationToken cancellationToken)
    }
}
