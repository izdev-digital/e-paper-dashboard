using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/homeassistant")]
[Authorize]
[DashboardOwner]
public class DashboardHomeAssistantController(
    HomeAssistantService homeAssistantService,
    ILogger<DashboardHomeAssistantController> logger) : ControllerBase
{
    private readonly HomeAssistantService _homeAssistantService = homeAssistantService;
    private readonly ILogger<DashboardHomeAssistantController> _logger = logger;

    [HttpGet("dashboards")]
    public async Task<IActionResult> GetDashboards(string dashboardId)
    {
        var result = await _homeAssistantService.FetchDashboards(dashboardId);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("entities")]
    public async Task<IActionResult> GetEntities(string dashboardId)
    {
        var result = await _homeAssistantService.FetchEntities(dashboardId);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("entity-states")]
    public async Task<IActionResult> GetEntityStates(string dashboardId, [FromBody] EntityIdsRequest request)
    {
        var result = await _homeAssistantService.FetchEntityStates(dashboardId, request.EntityIds ?? []);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("entity-history")]
    public async Task<IActionResult> GetEntityHistory(string dashboardId, [FromBody] EntityHistoryRequest request)
    {
        var hours = Clamp(request.Hours ?? 24, 1, 720);
        var result = await _homeAssistantService.FetchEntityHistory(dashboardId, request.EntityIds ?? [], hours);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("todo-items/{todoEntityId}")]
    public async Task<IActionResult> GetTodoItems(string dashboardId, string todoEntityId)
    {
        var result = await _homeAssistantService.FetchTodoItems(dashboardId, todoEntityId);
        return result.IsSuccess
            ? Ok(new { data = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("calendar-events/{calendarEntityId}")]
    public async Task<IActionResult> GetCalendarEvents(string dashboardId, string calendarEntityId, [FromQuery] int hoursAhead = 168)
    {
        hoursAhead = Clamp(hoursAhead, 1, 720);
        var result = await _homeAssistantService.FetchCalendarEvents(dashboardId, calendarEntityId, hoursAhead);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch calendar events: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} calendar events for entity {Entity}", result.Value.Count, calendarEntityId);
        return Ok(new { data = result.Value });
    }

    [HttpGet("weather-forecast/{weatherEntityId}")]
    public async Task<IActionResult> GetWeatherForecast(string dashboardId, string weatherEntityId, [FromQuery] string forecastType = "daily")
    {
        var result = await _homeAssistantService.FetchWeatherForecast(dashboardId, weatherEntityId, forecastType);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch weather forecast: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched weather forecast for entity {Entity}", weatherEntityId);
        return Ok(new { data = result.Value });
    }

    [HttpGet("rss-feed-entries/{feedEntityId}")]
    public async Task<IActionResult> GetRssFeedEntries(string dashboardId, string feedEntityId)
    {
        var result = await _homeAssistantService.FetchRssFeedEntries(dashboardId, feedEntityId);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch RSS feed entries: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} RSS feed entries for entity {Entity}", result.Value.Count, feedEntityId);
        return Ok(new { data = result.Value });
    }

    public record EntityIdsRequest(string[] EntityIds);
    public record EntityHistoryRequest(string[] EntityIds, int? Hours = 24);

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
