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

        [HttpGet("values")]
        public async Task<IActionResult> GetAllInformationAsync(CancellationToken cancellationToken)
        {
            var model = await _childNodeService.GetChildClusterInformationModelAsync(cancellationToken);
            return Ok(model);
        }

        [HttpPost("nodes")]
        public async Task<IActionResult> AddNodeAsync([FromBody] VirtualNode node, CancellationToken cancellationToken)
        {
            await _childNodeService.AddNodeAsync(node, cancellationToken);
            return Ok();
        }

        [HttpDelete("nodes/{position}")]
        public async Task<IActionResult> RemoveNodeAsync([FromRoute] uint position, CancellationToken cancellationToken)
        {
            await _childNodeService.RemoveNodeAsync(position, cancellationToken);
            return Ok();
        }

        [HttpGet("{nodePosition}/{keyHash}")]
        public async Task<string> GetValueAsync([FromRoute] uint nodePosition, [FromRoute] uint keyHash, CancellationToken cancellationToken)
        {
            var value = await _childNodeService.GetValueAsync(nodePosition, keyHash, cancellationToken);
            return value;
        }

        [HttpPost("{nodePosition}/{keyHash}")]
        public async Task<IActionResult> AddValueAsync([FromRoute] uint nodePosition, [FromRoute] uint keyHash, [FromBody] string value, CancellationToken cancellationToken)
        {
            var doesNeedRebalancing = await _childNodeService.AddValueAsync(nodePosition, keyHash, value, cancellationToken);
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
        public async Task RemoveFirstHalfOfCacheAsync([FromRoute] uint nodePosition, [FromQuery] uint lastKeyHashInclusively, CancellationToken cancellationToken)
        {
            await _childNodeService.RemoveFirstHalfOfCacheAsync(nodePosition, lastKeyHashInclusively, cancellationToken);
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