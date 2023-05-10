using DistributedCache.Common.Hashing;

namespace DistributedCache.Common
{
    public class HashingRing
    {
        private readonly IHashService _hashService;
        private readonly SortedList<uint, VirtualNode> _virtualNodes = new SortedList<uint, VirtualNode>();

        public HashingRing(IHashService hashService)
        {
            _hashService = hashService;
        }

        public uint MaxValue => _hashService.MaxHashValue;

        public void AddVirtualNode(VirtualNode virtualNode)
        {
            _virtualNodes.Add(virtualNode.RingPosition, virtualNode);
        }

        public VirtualNode GetVirtualNodeForHash(uint keyPosition)
        {
            var sortedNodePositions = _virtualNodes.Keys;
            var nodePosition = BinarySearchRightMostNode(sortedNodePositions, keyPosition);

            var node = _virtualNodes[nodePosition];

            return node;
        }

        public uint BinarySearchRightMostNode(IList<uint> nodePositions, uint keyPosition)
        {
            // in case keyPosition is bigger than MaxValue (if we consider to use real 360 degree circle or any other scale)
            // we should adjust it to max value of ring
            keyPosition = keyPosition % MaxValue;

            var start = 0;
            var end = nodePositions.Count - 1;

            while (start != end)
            {
                var mid = ((end - start) / 2) + start;

                if (keyPosition <= nodePositions[mid])
                {
                    end = mid;
                }
                else
                {
                    start = mid + 1;
                }
            }

            var nodePosition = nodePositions[start];

            // if your key is after node but before MaxHashValue - we return first node (because it is hash circle)
            if (keyPosition > nodePosition)
            {
                return nodePositions[0];
            }

            return nodePosition;
        }
    }
}
