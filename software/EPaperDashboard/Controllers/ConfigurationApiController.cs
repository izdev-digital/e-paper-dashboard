using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Utilities;
using EPaperDashboard.Services;
using CSharpFunctionalExtensions;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize(Policy = "ApiKeyPolicy")]
public class ConfigurationApiController(DashboardService dashboardService) : ControllerBase
{
    private readonly DashboardService _dashboardService = dashboardService;

    [HttpGet("next-update-wait-seconds")]
    public IActionResult GetNextUpdateWait([FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey)
    {
        var now = DateTime.Now;
        return _dashboardService
            .GetDashboardByApiKey(apiKey)
            .Select(d => d.UpdateTimes ?? [])
            .Bind(updateTimes => updateTimes
                    .Select(t => now.Date.Add(t.ToTimeSpan()))
                    .Where(dt => dt > now)
                    .OrderBy(dt => dt)
                    .TryFirst())
            .Match(
                nextUpdate => Ok((int)(nextUpdate - now).TotalSeconds),
                () => (IActionResult)NotFound("No upcoming update times found.")
            );
    }
}
