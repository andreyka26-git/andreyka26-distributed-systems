using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ClusterInformationModel
    {
        public Dictionary<PhysicalNode, LoadBalancerInformationModel> LoadBalancerInformations { get; set; } = new Dictionary<PhysicalNode, LoadBalancerInformationModel>();
    }
}
