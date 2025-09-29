using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ChildInformationModelItem
    {
        public VirtualNode Node { get; set; }
        public Dictionary<uint, string> CacheItems { get; set; } = new Dictionary<uint, string>();
    }
}
