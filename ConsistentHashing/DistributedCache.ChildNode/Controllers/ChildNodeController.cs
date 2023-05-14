using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.ChildNode.Controllers
{
    [ApiController]
    [Route("child-node")]
    public class ChildNodeController : ControllerBase
    {
        private readonly IChildNodeService _childNodeService;

        public ChildNodeController(IChildNodeService childNodeService)
        {
            _childNodeService = childNodeService;
        }

        [HttpPost("nodes")]
        public async Task<IActionResult> AddNodeAsync([FromBody] VirtualNode node, CancellationToken cancellationToken)
        {
            await _childNodeService.AddNodeAsync(node, cancellationToken);
            return Ok();
        }

        [HttpDelete("nodes/{position}")]
        public async Task<IActionResult> RemoveNodeAsync([FromRoute] uint position, CancellationToken cancellation)
        {
            await _childNodeService.RemoveNodeAsync(position, cancellation);
            return Ok();
        }

        [HttpGet("{nodePosition}/{hashKey}")]
        public async Task<string> GetValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, CancellationToken cancellationToken)
        {
            var value = await _childNodeService.GetValueAsync(nodePosition, hashKey, cancellationToken);
            return value;
        }

        [HttpPost("{nodePosition}/{hashKey}")]
        public async Task<IActionResult> AddValueAsync([FromRoute] uint nodePosition, [FromRoute] uint hashKey, [FromBody] string value, CancellationToken cancellationToken)
        {
            var doesNeedRebalancing = await _childNodeService.AddValueAsync(nodePosition, hashKey, value, cancellationToken);
            Response.Headers["Need-Rebalancing"] = doesNeedRebalancing.ToString();

            return Ok();
        }

        [HttpGet("{nodePosition}/firstHalf")]
        public async Task<Dictionary<uint, string>> GetFirstHalfOfCacheAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken)
        {
            var firstPart = await _childNodeService.GetFirstHalfOfCacheAsync(nodePosition, cancellationToken);
            return firstPart;
        }

        [HttpDelete("{nodePosition}/firstHalf")]
        public async Task RemoveFirstHalfOfCacheAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken)
        {
            await _childNodeService.RemoveFirstHalfOfCacheAsync(nodePosition, cancellationToken);
        }

        [HttpPost("{nodePosition}/bulk")]
        public async Task AddBulkToCacheAsync([FromRoute] uint nodePosition, [FromBody] Dictionary<uint, string> cacheItems, CancellationToken cancellationToken)
        {
            await _childNodeService.AddBulkToCacheAsync(nodePosition, cacheItems, cancellationToken);
        }

        [HttpGet("{nodePosition}/count")]
        public async Task<int> GetCountAsync([FromRoute] uint nodePosition, CancellationToken cancellationToken)
        {
            var count = await _childNodeService.GetCountAsync(nodePosition, cancellationToken);
            return count;
        }
    }
}