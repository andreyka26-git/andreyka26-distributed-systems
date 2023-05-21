using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ClusterInformationModelItem
    {
        public PhysicalNode PhysicalNode { get; set; }
        public LoadBalancerInformationModel LoadBalancerInfo { get; set; }
    }
}

