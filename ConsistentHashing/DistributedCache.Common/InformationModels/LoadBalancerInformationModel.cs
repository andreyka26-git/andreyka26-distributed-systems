using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class LoadBalancerInformationModel
    {
        public Dictionary<PhysicalNode, ChildInformationModel> ChildInformationModels { get; set; }
    }
}
