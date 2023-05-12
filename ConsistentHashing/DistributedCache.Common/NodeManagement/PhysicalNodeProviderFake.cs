namespace DistributedCache.Common.NodeManagement
{
    public class PhysicalNodeProviderFake : IPhysicalNodeProvider
    {
        public PhysicalNode CreateNewPhysicalNode()
        {
            var node = new PhysicalNode(new Uri($"https://physicalnode{new Random().Next(1,10000)}"));
            return node;
        }
    }
}
