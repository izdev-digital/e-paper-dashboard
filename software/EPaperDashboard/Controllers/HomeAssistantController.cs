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

    /// <summary>
    /// Fetches upcoming calendar events for a specific calendar entity.
    /// Fetches events for a full week by default to provide more event options.
    /// Display count is limited by widget's maxEvents configuration.
    /// </summary>
    /// <param name="dashboardId">Dashboard ID for authentication</param>
    /// <param name="calendarEntityId">Calendar entity ID (e.g., calendar.my_calendar)</param>
    /// <param name="hoursAhead">Number of hours into the future to fetch events (default: 168 = 7 days, max: 720)</param>
    [HttpGet("{dashboardId}/calendar-events/{calendarEntityId}")]
    public async Task<IActionResult> GetCalendarEvents(string dashboardId, string calendarEntityId, [FromQuery] int hoursAhead = 168)
    {
        // Validate hours ahead (max 30 days)
        if (hoursAhead < 1)
            hoursAhead = 1;
        if (hoursAhead > 720)
            hoursAhead = 720;

        var result = await _homeAssistantService.FetchCalendarEvents(dashboardId, calendarEntityId, hoursAhead);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch calendar events: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} calendar events for entity {Entity}", result.Value.Count, calendarEntityId);
        return Ok(new { events = result.Value });
    }
}
