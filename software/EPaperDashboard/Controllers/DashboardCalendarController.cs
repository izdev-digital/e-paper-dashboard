using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/calendar-events")]
[Authorize]
[DashboardOwner]
public class DashboardCalendarController(
    ICalendarDataProvider calendarDataProvider,
    ILogger<DashboardCalendarController> logger) : ControllerBase
{
    private readonly ICalendarDataProvider _calendarDataProvider = calendarDataProvider;
    private readonly ILogger<DashboardCalendarController> _logger = logger;

    /// <summary>
    /// Fetches upcoming calendar events for a specific calendar entity.
    /// Fetches events for a full week by default to provide more event options.
    /// Display count is limited by widget's maxEvents configuration.
    /// </summary>
    [HttpGet("{calendarEntityId}")]
    public async Task<IActionResult> GetCalendarEvents(string dashboardId, string calendarEntityId, [FromQuery] int hoursAhead = 168)
    {
        hoursAhead = Clamp(hoursAhead, 1, 720);
        var result = await _calendarDataProvider.FetchCalendarEventsAsync(dashboardId, calendarEntityId, hoursAhead, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch calendar events: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} calendar events for entity {Entity}", result.Value.Count, calendarEntityId);
        return Ok(new { data = result.Value });
    }

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
}
