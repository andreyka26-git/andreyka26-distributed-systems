namespace DistributedCache.Common.NodeManagement
{
    public interface INodeManager
    {
        VirtualNode GetVirtualNodeForHash(uint keyPosition);
        PhysicalNode ResolvePhysicalNode(VirtualNode virtualNode);
        void AddPhysicalNode(PhysicalNode physicalNode);
        void AddVirtualNode(VirtualNode virtualNode, PhysicalNode toPhysicalNode);
        void RemoveVirtualNode(VirtualNode virtualNode, PhysicalNode physicalNode);
    }
}
