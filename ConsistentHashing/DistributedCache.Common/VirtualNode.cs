namespace DistributedCache.Common
{
    public class VirtualNode
    {
        public Guid Id { get; } = Guid.NewGuid();

        public uint RingPosition { get; set; }
    }
}
