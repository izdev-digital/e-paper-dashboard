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
            .Bind(d => GetNextUpdateTime(now, d.UpdateTimes))
            .Match(
                nextUpdate => Ok((int)(nextUpdate - now).TotalSeconds),
                () => (IActionResult)NotFound("No upcoming update times found.")
            );

        Maybe<DateTime> GetNextUpdateTime(DateTime now, List<TimeOnly>? updateTimes)
        {
            if (updateTimes is null || !updateTimes.Any())
            {
                return Maybe.None;
            }

            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var times = updateTimes.OrderBy(t => t).ToList();
            return times
                .Select(t => today.Add(t.ToTimeSpan()))
                .Append(tomorrow.Add(times.First().ToTimeSpan()))
                .Where(dt => dt > now)
                .TryFirst();
        }
    }
}
