using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.Master.Controllers
{
    [ApiController]
    [Route("master")]
    public class MasterController : ControllerBase
    {
        private readonly IMasterService _masterService;

        public MasterController(IMasterService masterService)
        {
            _masterService = masterService;
        }

        [HttpPost("rebalance")]
        public async Task<IActionResult> RebalanceNodeAsync([FromBody] VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _masterService.RebalanceNodeAsync(hotVirtualNode, cancellationToken);

            return Ok();
        }
    }
}