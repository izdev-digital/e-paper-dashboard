using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Utilities;
using EPaperDashboard.Services;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize(Policy = "ApiKeyPolicy")]
public class ConfigurationApiController(DashboardService dashboardService) : ControllerBase
{
    private readonly DashboardService _dashboardService = dashboardService;

    [HttpGet("next-update-wait")]
    public IActionResult GetNextUpdateWait([FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Unauthorized("Missing API key.");
        var dashboard = _dashboardService.GetDashboardByApiKey(apiKey);
        if (dashboard == null || dashboard.UpdateTimes == null || dashboard.UpdateTimes.Count == 0)
            return NotFound("Dashboard or update times not found.");
        var now = DateTime.Now;
        var todayTimes = dashboard.UpdateTimes
            .Select(t => now.Date.Add(t.ToTimeSpan()))
            .Where(dt => dt > now)
            .OrderBy(dt => dt)
            .ToList();
        DateTime nextUpdate;
        if (todayTimes.Count > 0)
        {
            nextUpdate = todayTimes.First();
        }
        else
        {
            // Next update is the first time tomorrow
            nextUpdate = now.Date.AddDays(1).Add(dashboard.UpdateTimes.Min().ToTimeSpan());
        }
        var waitSeconds = (int)(nextUpdate - now).TotalSeconds;
        return Ok(new { waitSeconds, nextUpdate = nextUpdate.ToString("o") });
    }
}
