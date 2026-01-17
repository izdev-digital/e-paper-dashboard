using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/homeassistant")]
[Authorize]
public class HomeAssistantController(
    HomeAssistantService homeAssistantService,
    ILogger<HomeAssistantController> logger) : ControllerBase
{
    private readonly HomeAssistantService _homeAssistantService = homeAssistantService;
    private readonly ILogger<HomeAssistantController> _logger = logger;

    [HttpPost("fetch-dashboards")]
    public async Task<IActionResult> FetchDashboards([FromBody] FetchDashboardsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DashboardId))
        {
            return BadRequest(new { error = "Dashboard ID is required" });
        }

        var result = await _homeAssistantService.FetchDashboards(request.DashboardId);

        return result.IsSuccess
            ? Ok(new { dashboards = result.Value })
            : BadRequest(new { error = result.Error });
    }

    public record FetchDashboardsRequest(string DashboardId);
}
