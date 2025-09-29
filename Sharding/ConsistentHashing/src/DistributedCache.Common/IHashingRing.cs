using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common
{
    public interface IHashingRing
    {
        uint MaxValue { get; }

        void RemoveVirtualNode(uint nodePosition);
        void AddVirtualNode(VirtualNode virtualNode);
        VirtualNode GetVirtualNodeForHash(uint keyPosition);
    }
}
