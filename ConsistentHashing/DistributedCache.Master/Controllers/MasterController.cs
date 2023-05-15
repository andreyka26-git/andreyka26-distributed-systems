using DistributedCache.Common.InformationModels;
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

        [HttpGet("values")]
        public async Task<IActionResult> GetClusterInfoAsync(CancellationToken cancellationToken)
        {
            var clusterInformation = await _masterService.GetClusterInformationAsync(cancellationToken);

            return Ok(clusterInformation);
        }

        [HttpGet("create-new")]
        public async Task<IActionResult> CreateNewNodeAsync([FromQuery] int port, CancellationToken cancellationToken)
        {
            var node = await _masterService.CreateNewChildNodeAsync(port, cancellationToken);
            return Ok(node.Location);
        }

        [HttpPost("rebalance")]
        public async Task<IActionResult> RebalanceNodeAsync([FromBody] VirtualNode hotVirtualNode, CancellationToken cancellationToken)
        {
            await _masterService.RebalanceNodeAsync(hotVirtualNode, cancellationToken);

            return Ok();
        }
    }
}