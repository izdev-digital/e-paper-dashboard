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
    public async Task<IActionResult> StartAuth([FromBody] AuthRequest request)
    {
        // In HA add-on mode, if host is not specified or is the local instance, 
        // create a long-lived token directly via supervisor
        if (_deploymentStrategy.IsHomeAssistantAddon 
            && (string.IsNullOrWhiteSpace(request.Host) || request.Host == Constants.SupervisorCoreUrl))
        {
            ObjectId dashboardId;
            try
            {
                dashboardId = new ObjectId(request.DashboardId);
            }
            catch
            {
                return BadRequest(new { error = "Invalid dashboard ID" });
            }

            var dashboard = _dashboardService.GetDashboardById(dashboardId);
            if (!dashboard.HasValue)
            {
                return NotFound(new { error = "Dashboard not found" });
            }

            var clientName = $"EPaperDashboard-{dashboard.Value.Name}-{Guid.NewGuid():N}";
            var token = await _deploymentStrategy.CreateAccessTokenAsync(clientName);

            if (token == null)
            {
                return BadRequest(new { error = "Failed to create long-lived access token" });
            }

            // Update the dashboard with the new token and set host to supervisor core
            dashboard.Value.AccessToken = token;
            dashboard.Value.Host = Constants.SupervisorCoreUrl;
            _dashboardService.UpdateDashboard(dashboard.Value);

            _logger.LogInformation("Created long-lived token via supervisor for dashboard {DashboardId}", request.DashboardId);

            // Return success without authUrl (indicates direct token creation)
            return Ok(new
            {
                success = true,
                message = "Access token created successfully",
                directAuth = true
            });
        }

        // Otherwise use OAuth flow
        var result = _authService.StartAuth(request.Host, request.DashboardId);

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

        var result = await _authService.HandleCallback(code, state, error);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("OAuth callback failed: {Error}", result.Error);
            // Redirect to error page with message - use Url.Content to respect PathBase
            var errorMessage = Uri.EscapeDataString(result.Error ?? "Unknown error");
            return Redirect(Url.Content($"~/dashboards?error={errorMessage}"));
        }

        // Redirect to the Angular SPA dashboard edit page with OAuth params
        // Use Url.Content to respect PathBase for ingress support
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

