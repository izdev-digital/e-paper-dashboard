using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides weather forecast data for the weather-forecast widget.
/// </summary>
public interface IWeatherForecastProvider
{
    Task<Result<Dictionary<string, object?>, string>> FetchWeatherForecastAsync(string dashboardId, string entityId, string forecastType = "daily");

    /// <summary>
    /// Discovers all available weather entities and fetches forecasts for each.
    /// </summary>
    Task<Result<Dictionary<string, List<object?>>, string>> FetchAllWeatherForecastsAsync(string dashboardId, string forecastType = "daily");
}
