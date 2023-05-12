namespace DistributedCache.Common.NodeManagement
{
    public interface IPhysicalNodeProvider
    {
        PhysicalNode CreateNewPhysicalNode();
    }
}
