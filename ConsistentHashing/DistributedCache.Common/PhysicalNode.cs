namespace DistributedCache.Common
{
    public record PhysicalNode(Uri Location)
    {
        public Dictionary<uint, VirtualNode> VirtualNodes { get; set; } = new Dictionary<uint, VirtualNode>();
    }
}
