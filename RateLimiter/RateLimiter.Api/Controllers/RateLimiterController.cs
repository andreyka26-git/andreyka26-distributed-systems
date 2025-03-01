using Microsoft.AspNetCore.Mvc;

namespace RateLimiter.Api.Controllers;

[ApiController]
[Route("rate-limiter")]
public class RateLimiterController : ControllerBase
{
    private readonly ILogger<RateLimiterController> _logger;

    public RateLimiterController(ILogger<RateLimiterController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        _logger.LogInformation("Received GET request");
        return Ok(new { message = "Rate Limiter API is working!" });
    }
} 