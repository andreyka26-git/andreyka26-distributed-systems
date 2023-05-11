using DistributedCache.Common;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.LoadBalancer.Controllers
{
    [ApiController]
    [Route("load-balancer")]
    public class LoadBalancerController : ControllerBase
    {
        private readonly INodeManager _nodeManager;
        private readonly IHashService _hashService;
        private readonly IHashingRing _hashingRing;
        private readonly IChildNodeService _childNodeService;

        public LoadBalancerController(
            INodeManager nodeManager,
            IHashService hashService,
            IHashingRing hashingRing,
            IChildNodeService childNodeService)
        {
            _nodeManager = nodeManager;
            _hashService = hashService;
            _hashingRing = hashingRing;
            _childNodeService = childNodeService;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetValueAsync(string key, CancellationToken cancellationToken)
        {
            var hashKey = _hashService.GetHash(key);

            var virtualNode = _hashingRing.GetVirtualNodeForHash(hashKey);
            var physicalNode = _nodeManager.ResolvePhysicalNode(virtualNode);

            var value = await _childNodeService.GetFromCacheAsync(hashKey, virtualNode, physicalNode, cancellationToken);
            return Ok(value);
        }
    }
}