using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class LoadBalancerInformationModelItem
    {
        public PhysicalNode PhysicalNode { get; set; }
        public ChildInformationModel ChildInfo { get; set; }
    }
}
