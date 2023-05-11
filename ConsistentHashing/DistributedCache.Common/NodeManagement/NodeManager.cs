﻿namespace DistributedCache.Common.NodeManagement
{
    public class NodeManager : INodeManager
    {
        private readonly Dictionary<VirtualNode, PhysicalNode> _virtualToPhysicalMapping = new Dictionary<VirtualNode, PhysicalNode>();

        // uint is a ring position, we agreed that it is unique identifier of the virtual node.
        private readonly Dictionary<PhysicalNode, Dictionary<uint, VirtualNode>> _physicalToVirtualMapping = new Dictionary<PhysicalNode, Dictionary<uint, VirtualNode>>();

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
            _physicalToVirtualMapping[toPhysicalNode][virtualNode.RingPosition] = virtualNode;
        }
    }
}