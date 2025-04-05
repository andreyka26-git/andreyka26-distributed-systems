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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public RateLimiterController(
        ILogger<RateLimiterController> logger,
        IRateLimiter rateLimiter,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _rateLimiter = rateLimiter;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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

        using var httpClient = _httpClientFactory.CreateClient();
        var baseUrl = _configuration.GetValue<string>("ProductionServiceBaseUrl");
        var throttled = false;

        try
        {
            await _rateLimiter.AllowAsync(callerId, now);
            return Ok(new { message = "Rate Limiter API is working!" });
        }
        catch (RateLimitException e)
        {
            throttled = true;
            _logger.LogError(e, "Rate limit exception occurred");
            return StatusCode(429, new { message = "Too Many Requests" });
        }
        finally
        {
            // not awaited intentionally
            httpClient.PostAsync($"{baseUrl}/production-service/request?userId={callerId}&throttled={throttled}", null);
        }
    }
} 