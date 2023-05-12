using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace DistributedCache.Master.Controllers
{
    [ApiController]
    [Route("master")]
    public class MasterController : ControllerBase
    {
        private readonly IChildNodeClient _childClient;
        private readonly INodeManager _nodeManager;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;
        private readonly List<ILoadBalancerNodeClient> _loadBalancers;

        public MasterController(
            IChildNodeClient childClient,
            INodeManager nodeManager,
            IPhysicalNodeProvider physicalNodeProvider,
            List<ILoadBalancerNodeClient> loadBalancers)
        {
            _childClient = childClient;
            _nodeManager = nodeManager;
            _physicalNodeProvider = physicalNodeProvider;
            _loadBalancers = loadBalancers;
        }

        // TODO make it serializable
        // TODO make it serializable
        // TODO make it serializable
        // TODO make it serializable
        [HttpPost("rebalance")]
        public async Task<IActionResult> RebalanceNodeAsync([FromBody] VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            var hotPhysicalNode = _nodeManager.ResolvePhysicalNode(hotVirtualNode);

            var newPhysicalNode = _physicalNodeProvider.CreateNewPhysicalNode();
            var firstHalf = await _childClient.GetFirstHalfOfCacheAsync(hotVirtualNode, hotPhysicalNode, cancellationToken);

            var nodePosition = firstHalf.Last().Key;
            var newVirtualNode = new VirtualNode(nodePosition);

            foreach (var loadBalacer in _loadBalancers)
            {
                await loadBalacer.AddVirtualNodeAsync(newVirtualNode, cancellationToken);
            }

            await _childClient.AddNewVirtualNodeAsync(newPhysicalNode, newVirtualNode, cancellationToken);
            await _childClient.AddFirstHalfToNewNodeAsync(firstHalf, newVirtualNode, newPhysicalNode, cancellationToken);

            await _childClient.RemoveFirstHalfOfCache(nodePosition, hotVirtualNode, hotPhysicalNode, cancellationToken);

            return Ok();
        }
    }
}