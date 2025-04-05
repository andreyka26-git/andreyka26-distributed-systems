using Microsoft.AspNetCore.Mvc;
using RateLimiter.Api.Application;

namespace RateLimiter.Api.Controllers;

[ApiController]
[Route("production-service")]
public class ProductionServiceController : ControllerBase
{
    private readonly IProductionService _productionService;

    public ProductionServiceController(
        IProductionService productionService)
    {
        _productionService = productionService;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot()
    {
        var requests = await _productionService.GetSerializedRequests();
        return Ok(requests);
    }

    [HttpPost("request")]
    public async Task<IActionResult> PerformRequest([FromQuery] string? userId, bool throttled = false)
    {
        var now = DateTime.UtcNow;
        var callerId = userId;

        if (string.IsNullOrEmpty(callerId))
        {
            callerId = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        if (throttled)
        {
            await _productionService.PerformThrottledRequest(callerId, now);
        }
        else
        {
            await _productionService.PerformRequest(callerId, now);
        }

        return Ok();
    }
}