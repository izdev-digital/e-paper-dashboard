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

    [HttpPost("fetch-entities")]
    public async Task<IActionResult> FetchEntities([FromBody] FetchEntitiesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DashboardId))
        {
            return BadRequest(new { error = "Dashboard ID is required" });
        }

        var result = await _homeAssistantService.FetchEntities(request.DashboardId);

        return result.IsSuccess
            ? Ok(new { entities = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("fetch-entity-states")]
    public async Task<IActionResult> FetchEntityStates([FromBody] FetchEntityStatesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DashboardId))
        {
            return BadRequest(new { error = "Dashboard ID is required" });
        }

        var result = await _homeAssistantService.FetchEntityStates(request.DashboardId, request.EntityIds ?? []);

        return result.IsSuccess
            ? Ok(new { states = result.Value })
            : BadRequest(new { error = result.Error });
    }

    public record FetchDashboardsRequest(string DashboardId);
    public record FetchEntitiesRequest(string DashboardId);
    public record FetchEntityStatesRequest(string DashboardId, string[] EntityIds);

    [HttpGet("{dashboardId}/todo-items/{todoEntityId}")]
    public async Task<IActionResult> GetTodoItems(string dashboardId, string todoEntityId)
    {
        var result = await _homeAssistantService.FetchTodoItems(dashboardId, todoEntityId);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(result.Value);
    }
}
