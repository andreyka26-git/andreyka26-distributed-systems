using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ChildInformationModel
    {
        public Dictionary<VirtualNode, Dictionary<uint, string>> VirtualNodesWithItems { get; set; } = new Dictionary<VirtualNode, Dictionary<uint, string>>();
    }
}
