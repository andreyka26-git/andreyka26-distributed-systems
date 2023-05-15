namespace DistributedCache.Common.NodeManagement
{
    public interface IPhysicalNodeProvider
    {
        IReadOnlyList<PhysicalNode> LoadBalancers { get; }
        IReadOnlyList<PhysicalNode> ChildNodes { get; }

        Task<PhysicalNode> CreateChildPhysicalNodeAsync(int? port = default, CancellationToken cancellationToken = default);
        Task<PhysicalNode> CreateLoadBalancerPhysicalNodeAsync(int? port = default, CancellationToken cancellationToken = default);
    }
}
