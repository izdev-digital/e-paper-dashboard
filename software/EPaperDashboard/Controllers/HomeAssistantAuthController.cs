using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;
using EPaperDashboard.Utilities;
using LiteDB;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/homeassistant")]
public class HomeAssistantAuthController(
    HomeAssistantAuthService authService,
    IDeploymentStrategy deploymentStrategy,
    DashboardService dashboardService,
    ILogger<HomeAssistantAuthController> logger) : ControllerBase
{
    private readonly HomeAssistantAuthService _authService = authService;
    private readonly IDeploymentStrategy _deploymentStrategy = deploymentStrategy;
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly ILogger<HomeAssistantAuthController> _logger = logger;

    [HttpPost("start-auth")]
    [Authorize]
    [DashboardOwnerFromBody]
    public IActionResult StartAuth([FromBody] AuthRequest request)
    {
        var host = request.Host;
        if (_deploymentStrategy.IsHomeAssistantAddon && string.IsNullOrWhiteSpace(host))
        {
            host = Constants.HomeAssistantCoreUrl;
        }

        var result = _authService.StartAuth(host, request.DashboardId, HttpContext);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            authUrl = result.AuthUrl,
            state = result.State,
            directAuth = false
        });
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        _logger.LogInformation("OAuth callback received - code: {Code}, state: {State}, error: {Error}", 
            code != null ? "present" : "null", 
            state != null ? "present" : "null",
            error ?? "none");

        var result = await _authService.HandleCallback(code, state, error, HttpContext);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("OAuth callback failed: {Error}", result.Error);
            var errorMessage = Uri.EscapeDataString(result.Error ?? "Unknown error");
            return Redirect(Url.Content($"~/dashboards?error={errorMessage}"));
        }

        var accessToken = Uri.EscapeDataString(result.AccessToken ?? "");
        var redirectUrl = Url.Content($"~/dashboards/{result.DashboardId}/edit?auth_callback=true&access_token={accessToken}");
        _logger.LogInformation("OAuth callback successful, redirecting to: {RedirectUrl}", redirectUrl);
        return Redirect(redirectUrl);
    }

    public class AuthRequest
    {
        public string Host { get; set; } = string.Empty;
        public string DashboardId { get; set; } = string.Empty;
    }
}

