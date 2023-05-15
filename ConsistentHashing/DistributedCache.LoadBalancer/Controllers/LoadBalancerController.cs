using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.LoadBalancer.Controllers
{
    //TODO decouple hashing ring to node manager.
    [ApiController]
    [Route("load-balancer")]
    public class LoadBalancerController : ControllerBase
    {
        private readonly ILoadBalancerService _loadBalancerService;

        public LoadBalancerController(ILoadBalancerService loadBalancerService)
        {
            _loadBalancerService = loadBalancerService;
        }

        [HttpPost("node")]
        public async Task AddVirtualNodeAsync([FromQuery] string physicalNodeUrl, [FromBody] VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await _loadBalancerService.AddVirtualNodeAsync(physicalNodeUrl, virtualNode, cancellationToken);
        }

        [HttpDelete("node")]
        public async Task RemoveVirtualNodeAsync([FromQuery] string physicalNodeUrl, [FromBody] VirtualNode virtualNode, CancellationToken cancellationToken)
        {
            await _loadBalancerService.RemoveVirtualNodeAsync(physicalNodeUrl, virtualNode, cancellationToken);
        }

        [HttpGet("{key}")]
        public async Task<IActionResult> GetValueAsync([FromRoute] string key, CancellationToken cancellationToken)
        {
            var value = await _loadBalancerService.GetValueAsync(key, cancellationToken);
            return Ok(value);
        }

        [HttpPost("{key}")]
        public async Task<IActionResult> AddValueAsync([FromRoute] string key, [FromBody] string value, CancellationToken cancellationToken)
        {
            await _loadBalancerService.AddValueAsync(key, value, cancellationToken);
            return Ok();
        }
    }
}