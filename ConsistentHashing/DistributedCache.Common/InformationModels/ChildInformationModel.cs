using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ChildInformationModel
    {
        public List<(VirtualNode, Dictionary<uint, string>)> VirtualNodesWithItems { get; set; } = new List<(VirtualNode, Dictionary<uint, string>)>();
    }
}
