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

        public VirtualNode GetVirtualNodeForHash(uint ringPosition)
        {
            
            var sortedPositions = _virtualNodes.Keys;

            var position = BinarySearchNearest(sortedPositions, ringPosition);
        }

        // handle [0, 1, 8, max]; [0, 1, max]
        public uint BinarySearchNearest(IList<uint> positions, uint position)
        {
            var start = 0;
            var end = positions.Count - 1;

            while (start != end)
            {
                var mid = (end - start) / 2;

                if (position <= positions[mid])
                {
                    end = mid;
                }
                else
                {
                    start = mid + 1;
                }
            }

            return positions[start];
        }
    }
}
