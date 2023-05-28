using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class ClusterInformationModel
    {
        public List<ClusterInformationModelItem> LoadBalancerInformations { get; set; } = new List<ClusterInformationModelItem>();
    }
}
