namespace DistributedCache.Common.NodeManagement
{
    public class ChildNodeManager : IChildNodeManager
    {
        private readonly Dictionary<VirtualNode, PhysicalNode> _virtualToPhysicalMapping = new Dictionary<VirtualNode, PhysicalNode>();

        // uint is a ring position, we agreed that it is unique identifier of the virtual node.
        private readonly Dictionary<PhysicalNode, Dictionary<uint, VirtualNode>> _physicalToVirtualMapping = new Dictionary<PhysicalNode, Dictionary<uint, VirtualNode>>();

        private readonly IHashingRing _hashingRing;

        public ChildNodeManager(IHashingRing hashingRing)
        {
            _hashingRing = hashingRing;
        }

        public VirtualNode GetVirtualNodeForHash(uint keyPosition)
        {
            return _hashingRing.GetVirtualNodeForHash(keyPosition);
        }

        public PhysicalNode ResolvePhysicalNode(VirtualNode virtualNode)
        {
            return _virtualToPhysicalMapping[virtualNode];
        }

        public void AddPhysicalNode(PhysicalNode physicalNode)
        {
            _physicalToVirtualMapping.Add(physicalNode, new Dictionary<uint, VirtualNode>());
        }

        public void AddVirtualNode(VirtualNode virtualNode, PhysicalNode toPhysicalNode)
        {
            _virtualToPhysicalMapping[virtualNode] = toPhysicalNode;

            if (!_physicalToVirtualMapping.ContainsKey(toPhysicalNode))
            {
                AddPhysicalNode(toPhysicalNode);
            }

            _physicalToVirtualMapping[toPhysicalNode][virtualNode.RingPosition] = virtualNode;

            _hashingRing.AddVirtualNode(virtualNode);
        }

        public void RemoveVirtualNode(VirtualNode virtualNode, PhysicalNode physicalNode)
        {
            _virtualToPhysicalMapping.Remove(virtualNode);

            var virtualNodes = _physicalToVirtualMapping[physicalNode];
            virtualNodes.Remove(virtualNode.RingPosition);

            _hashingRing.RemoveVirtualNode(virtualNode.RingPosition);
        }
    }
}
