using DistributedCache.Common.NodeManagement;
using Microsoft.AspNetCore.Mvc;

namespace DistributedCache.Master.Controllers
{
    [ApiController]
    [Route("master")]
    public class MasterController : ControllerBase
    {
        private readonly IMasterService _masterService;
        private readonly IPhysicalNodeProvider _physicalNodeProvider;

        public MasterController(IMasterService masterService, IPhysicalNodeProvider physicalNodeProvider)
        {
            _masterService = masterService;
            _physicalNodeProvider = physicalNodeProvider;
        }

        [HttpGet("create-new")]
        public async Task<IActionResult> CreateNewNodeAsync(CancellationToken cancellationToken)
        {
            await _physicalNodeProvider.CreateNewPhysicalNodeAsync(cancellationToken);
            return Ok();
        }

        [HttpPost("rebalance")]
        public async Task<IActionResult> RebalanceNodeAsync([FromBody] VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _masterService.RebalanceNodeAsync(hotVirtualNode, cancellationToken);

            return Ok();
        }
    }
}