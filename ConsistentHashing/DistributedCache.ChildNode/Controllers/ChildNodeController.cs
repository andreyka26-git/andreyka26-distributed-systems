using DistributedCache.Common;
using DistributedCache.Common.Cache;
using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace DistributedCache.ChildNode.Controllers
{
    [ApiController]
    [Route("child-node")]
    public class ChildNodeController : ControllerBase
    {
        private readonly IVirtualNodeManager _nodeManager;
        private readonly IMasterNodeClient _masterNodeClient;

        public ChildNodeController(
            IVirtualNodeManager nodeManager,
            IMasterNodeClient masterClient)
        {
            _nodeManager = nodeManager;
            _masterNodeClient = masterClient;
        }

        [HttpPost("nodes")]
        public async Task<IActionResult> AddNodeAsync([FromBody] VirtualNode node, CancellationToken cancellation)
        {
            _nodeManager.NodeToCacheMapping.Add(node.RingPosition, (node, new ChildNodeInMemoryCache(node.MaxItemsCount)));
            return Ok();
        }

        [HttpDelete("nodes/{position}")]
        public async Task<IActionResult> RemoveNodeAsync([FromRoute] uint position, CancellationToken cancellation)
        {
            _nodeManager.NodeToCacheMapping.Remove(position);
            return Ok();
        }

        [HttpGet("{nodePosition}/{hashKey}")]
        public async Task<IActionResult> GetValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, CancellationToken cancellationToken)
        {
            var value = _nodeManager.NodeToCacheMapping[nodePosition].Cache.GetFromCache(hashKey);
            return Ok(value);
        }

        [HttpPost("{nodePosition}/{hashKey}")]
        public async Task<IActionResult> AddValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, [FromBody] string value, CancellationToken cancellationToken)
        {
            var doesNeedRebalancing = _nodeManager.NodeToCacheMapping[nodePosition].Cache.AddToCache(hashKey, value);
            Response.Headers["Need-Rebalancing"] = doesNeedRebalancing.ToString();

            if (doesNeedRebalancing)
            {
                await _masterNodeClient.EmitNodeRebalancingAsync(_nodeManager.NodeToCacheMapping[nodePosition].Node, cancellationToken);
            }

            return Ok();
        }

        [HttpGet("{nodePosition}/count")]
        public async Task<IActionResult> GetCountAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken)
        {
            var count = _nodeManager.NodeToCacheMapping[nodePosition].Cache.GetCountOfItems();
            return Ok(count);
        }
    }
}