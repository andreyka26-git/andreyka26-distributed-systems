using Microsoft.AspNetCore.Mvc;
using RateLimiter.Api.Application;
using RateLimiter.Api.Infrastructure;

namespace RateLimiter.Api.Controllers;

[ApiController]
[Route("rate-limiter")]
public class RateLimiterController : ControllerBase
{
    private readonly ILogger<RateLimiterController> _logger;
    private readonly IRateLimiter _rateLimiter;
    private readonly ProductionService _productionService;

    public RateLimiterController(
        ILogger<RateLimiterController> logger,
        IRateLimiter rateLimiter,
        ProductionService productionService)
    {
        _logger = logger;
        _rateLimiter = rateLimiter;
        _productionService = productionService;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot()
    {
       var requests = _productionService.GetSerializedRequests(); 
       return Ok(requests);
    }

    [HttpGet]
    public async Task<IActionResult> PerformRequest([FromQuery] string? userId)
    {
        var now = DateTime.UtcNow;
        var callerId = userId;

        if (string.IsNullOrEmpty(callerId))
        {
            callerId = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        try
        {
            await _rateLimiter.AllowAsync(callerId, now);
            _productionService.PerformRequest(callerId, now);

            return Ok(new { message = "Rate Limiter API is working!" });
        }
        catch (RateLimitException e)
        {
            _productionService.PerformThrottledRequest(callerId, now);
            
            _logger.LogError(e, "Rate limit exception occurred");
            return StatusCode(429, new { message = "Too Many Requests" });
        }
    }
} 