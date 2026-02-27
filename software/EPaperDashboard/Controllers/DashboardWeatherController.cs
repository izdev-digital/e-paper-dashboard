using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/weather-forecast")]
[Authorize]
[DashboardOwner]
public class DashboardWeatherController(
    IWeatherForecastProvider weatherForecastProvider,
    ILogger<DashboardWeatherController> logger) : ControllerBase
{
    private readonly IWeatherForecastProvider _weatherForecastProvider = weatherForecastProvider;
    private readonly ILogger<DashboardWeatherController> _logger = logger;

    /// <summary>
    /// Fetches weather forecast data for a weather entity.
    /// </summary>
    [HttpGet("{weatherEntityId}")]
    public async Task<IActionResult> GetWeatherForecast(string dashboardId, string weatherEntityId, [FromQuery] string forecastType = "daily")
    {
        var result = await _weatherForecastProvider.FetchWeatherForecastAsync(dashboardId, weatherEntityId, forecastType);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to fetch weather forecast: {Error}", result.Error);
            return BadRequest(new { error = result.Error });
        }

        _logger.LogInformation("Successfully fetched weather forecast for entity {Entity}", weatherEntityId);
        return Ok(new { data = result.Value });
    }
}
