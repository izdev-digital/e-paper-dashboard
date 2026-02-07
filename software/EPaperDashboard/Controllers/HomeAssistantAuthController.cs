using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/homeassistant")]
public class HomeAssistantAuthController(
    HomeAssistantAuthService authService,
    ILogger<HomeAssistantAuthController> logger) : ControllerBase
{
    private readonly HomeAssistantAuthService _authService = authService;
    private readonly ILogger<HomeAssistantAuthController> _logger = logger;

    [HttpPost("start-auth")]
    [Authorize]
    [DashboardOwnerFromBody]
    public IActionResult StartAuth([FromBody] AuthRequest request)
    {
        var result = _authService.StartAuth(request.Host, request.DashboardId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            authUrl = result.AuthUrl,
            state = result.State
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
            // Redirect to error page with message
            var errorMessage = Uri.EscapeDataString(result.Error ?? "Unknown error");
            return Redirect($"/dashboards?error={errorMessage}");
        }

        // Redirect to the Angular SPA dashboard edit page with OAuth params
        var accessToken = Uri.EscapeDataString(result.AccessToken ?? "");
        var redirectUrl = $"/dashboards/{result.DashboardId}/edit?auth_callback=true&access_token={accessToken}";
        _logger.LogInformation("OAuth callback successful, redirecting to: {RedirectUrl}", redirectUrl);
        return Redirect(redirectUrl);
    }

    public class AuthRequest
    {
        public string Host { get; set; } = string.Empty;
        public string DashboardId { get; set; } = string.Empty;
    }
}

