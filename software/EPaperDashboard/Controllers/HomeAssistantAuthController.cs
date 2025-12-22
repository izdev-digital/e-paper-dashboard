using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/homeassistant")]
[Authorize]
public class HomeAssistantAuthController(
    HomeAssistantAuthService authService,
    ILogger<HomeAssistantAuthController> logger) : ControllerBase
{
    private readonly HomeAssistantAuthService _authService = authService;
    private readonly ILogger<HomeAssistantAuthController> _logger = logger;

    [HttpPost("start-auth")]
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

    public class AuthRequest
    {
        public string Host { get; set; } = string.Empty;
        public string DashboardId { get; set; } = string.Empty;
    }
}
