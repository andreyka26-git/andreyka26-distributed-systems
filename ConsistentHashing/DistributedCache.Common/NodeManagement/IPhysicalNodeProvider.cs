namespace DistributedCache.Common.NodeManagement
{
    public interface IPhysicalNodeProvider
    {
        Task<PhysicalNode> CreateNewPhysicalNodeAsync();
    }
}
