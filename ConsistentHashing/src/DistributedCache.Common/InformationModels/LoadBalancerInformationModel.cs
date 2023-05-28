using DistributedCache.Common.NodeManagement;

namespace DistributedCache.Common.InformationModels
{
    public class LoadBalancerInformationModel
    {
        public List<LoadBalancerInformationModelItem> ChildInformationModels { get; set; } = new List<LoadBalancerInformationModelItem>();
    }
}
