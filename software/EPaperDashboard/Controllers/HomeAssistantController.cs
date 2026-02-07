using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Guards;

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
    [DashboardOwnerFromBody]
    public async Task<IActionResult> FetchDashboards([FromBody] FetchDashboardsRequest request)
    {
        var result = await _homeAssistantService.FetchDashboards(request.DashboardId);

        return result.IsSuccess
            ? Ok(new { dashboards = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("fetch-entities")]
    [DashboardOwnerFromBody]
    public async Task<IActionResult> FetchEntities([FromBody] FetchEntitiesRequest request)
    {
        var result = await _homeAssistantService.FetchEntities(request.DashboardId);

        return result.IsSuccess
            ? Ok(new { entities = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("fetch-entity-states")]
    [DashboardOwnerFromBody]
    public async Task<IActionResult> FetchEntityStates([FromBody] FetchEntityStatesRequest request)
    {
        var result = await _homeAssistantService.FetchEntityStates(request.DashboardId, request.EntityIds ?? []);

        return result.IsSuccess
            ? Ok(new { states = result.Value })
            : BadRequest(new { error = result.Error });
    }

    public record FetchDashboardsRequest(string DashboardId);
    public record FetchEntitiesRequest(string DashboardId);
    public record FetchEntityStatesRequest(string DashboardId, string[] EntityIds);
    public record FetchEntityHistoryRequest(string DashboardId, string[] EntityIds, int? Hours = 24);

    [HttpPost("fetch-entity-history")]
    [DashboardOwnerFromBody]
    public async Task<IActionResult> FetchEntityHistory([FromBody] FetchEntityHistoryRequest request)
    {
        var hours = request.Hours ?? 24;
        if (hours < 1)
            hours = 1;
        if (hours > 720) // Max 30 days
            hours = 720;

        var result = await _homeAssistantService.FetchEntityHistory(request.DashboardId, request.EntityIds ?? [], hours);

        return result.IsSuccess
            ? Ok(new { history = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("{dashboardId}/todo-items/{todoEntityId}")]
    [DashboardOwner]
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
    [DashboardOwner]
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

    /// <summary>
    /// Fetches weather forecast data for a weather entity.
    /// Uses the weather.get_forecasts service to retrieve forecast data.
    /// </summary>
    /// <param name="dashboardId">Dashboard ID for authentication</param>
    /// <param name="weatherEntityId">Weather entity ID (e.g., weather.openmeteo_home)</param>
    /// <param name="forecastType">Type of forecast: 'daily', 'hourly', or 'twice_daily' (default: 'daily')</param>
    [HttpGet("{dashboardId}/weather-forecast/{weatherEntityId}")]
    [DashboardOwner]
    public async Task<IActionResult> GetWeatherForecast(string dashboardId, string weatherEntityId, [FromQuery] string forecastType = "daily")
    {
        var result = await _homeAssistantService.FetchWeatherForecast(dashboardId, weatherEntityId, forecastType);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch weather forecast: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched weather forecast for entity {Entity}", weatherEntityId);
        return Ok(result.Value);
    }

    /// <summary>
    /// Fetches RSS feed entries from a Home Assistant feedreader entity.
    /// The feedreader component stores RSS entries in the entity's attributes.
    /// </summary>
    /// <param name="dashboardId">Dashboard ID for authentication</param>
    /// <param name="feedEntityId">Feed entity ID (e.g., feedreader.my_feed)</param>
    [HttpGet("{dashboardId}/rss-feed-entries/{feedEntityId}")]
    [DashboardOwner]
    public async Task<IActionResult> GetRssFeedEntries(string dashboardId, string feedEntityId)
    {
        var result = await _homeAssistantService.FetchRssFeedEntries(dashboardId, feedEntityId);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch RSS feed entries: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} RSS feed entries for entity {Entity}", result.Value.Count, feedEntityId);
        return Ok(new { entries = result.Value });
    }
}
