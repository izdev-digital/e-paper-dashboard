using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/rss-feed-entries")]
[Authorize]
[DashboardOwner]
public class DashboardRssFeedController(
    IRssFeedDataProvider rssFeedDataProvider,
    ILogger<DashboardRssFeedController> logger) : ControllerBase
{
    private readonly IRssFeedDataProvider _rssFeedDataProvider = rssFeedDataProvider;
    private readonly ILogger<DashboardRssFeedController> _logger = logger;

    /// <summary>
    /// Fetches RSS feed entries from a feedreader entity.
    /// </summary>
    [HttpGet("{feedEntityId}")]
    public async Task<IActionResult> GetRssFeedEntries(string dashboardId, string feedEntityId)
    {
        var result = await _rssFeedDataProvider.FetchRssFeedEntriesAsync(dashboardId, feedEntityId);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch RSS feed entries: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched {Count} RSS feed entries for entity {Entity}", result.Value.Count, feedEntityId);
        return Ok(new { data = result.Value });
    }
}
