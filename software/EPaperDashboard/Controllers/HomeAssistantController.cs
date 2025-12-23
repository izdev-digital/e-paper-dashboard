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
    public async Task<IActionResult> FetchDashboards([FromBody] string dashboardId)
    {
        var result = await _homeAssistantService.FetchDashboards(dashboardId);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            dashboards = result.Value
        });
    }
}
