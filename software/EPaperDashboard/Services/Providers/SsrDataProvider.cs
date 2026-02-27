using System.Text.Json;
using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Default implementation of <see cref="ISsrDataProvider"/> that orchestrates
/// per-widget data providers to collect all data needed for server-side rendering.
/// </summary>
public sealed class SsrDataProvider(
    IEntityStateProvider entityStateProvider,
    ITodoDataProvider todoDataProvider,
    ICalendarDataProvider calendarDataProvider,
    IWeatherForecastProvider weatherForecastProvider,
    IRssFeedDataProvider rssFeedDataProvider,
    IEntityHistoryProvider entityHistoryProvider,
    ILogger<SsrDataProvider> logger) : ISsrDataProvider
{
    private readonly IEntityStateProvider _entityStateProvider = entityStateProvider;
    private readonly ITodoDataProvider _todoDataProvider = todoDataProvider;
    private readonly ICalendarDataProvider _calendarDataProvider = calendarDataProvider;
    private readonly IWeatherForecastProvider _weatherForecastProvider = weatherForecastProvider;
    private readonly IRssFeedDataProvider _rssFeedDataProvider = rssFeedDataProvider;
    private readonly IEntityHistoryProvider _entityHistoryProvider = entityHistoryProvider;
    private readonly ILogger<SsrDataProvider> _logger = logger;

    public async Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout)
    {
        var data = new SsrData();

        // Collect all entity IDs needed across all widgets
        var entityIds = CollectEntityIds(layout);

        // Fetch all entity states in one call
        if (entityIds.Count > 0)
        {
            var statesResult = await _entityStateProvider.FetchEntityStatesAsync(dashboardId, entityIds.ToArray());
            if (statesResult.IsSuccess)
            {
                foreach (var state in statesResult.Value)
                    data.EntityStates[state.EntityId] = state;
            }
            else
            {
                _logger.LogWarning("SSR: Failed to fetch entity states: {Error}", statesResult.Error);
            }
        }

        // Fetch todo items per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "todo"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _todoDataProvider.FetchTodoItemsAsync(dashboardId, entityId);
                if (result.IsSuccess) data.TodoItems[entityId] = result.Value;
            }
        }

        // Fetch calendar events per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "calendar"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _calendarDataProvider.FetchCalendarEventsAsync(dashboardId, entityId, 168);
                if (result.IsSuccess) data.CalendarEvents[entityId] = result.Value;
            }
        }

        // Fetch weather forecasts per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "weather-forecast"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
            var forecastType = forecastMode == "hourly" ? "hourly" : "daily";
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _weatherForecastProvider.FetchWeatherForecastAsync(dashboardId, entityId, forecastType);
                if (result.IsSuccess
                    && result.Value.TryGetValue("forecast", out var forecastVal)
                    && forecastVal is List<object?> forecastList)
                {
                    data.WeatherForecasts[entityId] = forecastList;
                }
            }
        }

        // Fetch RSS feed entries per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "rss-feed"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _rssFeedDataProvider.FetchRssFeedEntriesAsync(dashboardId, entityId);
                if (result.IsSuccess)
                {
                    data.RssFeedEntries[entityId] = result.Value;
                    _logger.LogDebug("SSR: Fetched {Count} RSS entries for {EntityId}", result.Value.Count, entityId);
                }
                else
                {
                    _logger.LogWarning("SSR: Failed to fetch RSS entries for {EntityId}: {Error}", entityId, result.Error);
                }
            }
        }

        // Fetch entity history for graph widgets
        foreach (var widget in layout.Widgets.Where(w => w.Type == "graph"))
        {
            if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
            {
                var graphEntityIds = series.EnumerateArray()
                    .Select(s => GetStringProp(s, "entityId"))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();

                if (graphEntityIds.Count > 0)
                {
                    var periodStr = GetStringProp(widget.Config, "period") ?? "24h";
                    var hours = periodStr switch
                    {
                        "1h" => 1,
                        "6h" => 6,
                        "24h" => 24,
                        "7d" => 168,
                        "30d" => 720,
                        _ => 24
                    };

                    var result = await _entityHistoryProvider.FetchEntityHistoryAsync(dashboardId, graphEntityIds, hours);
                    if (result.IsSuccess)
                    {
                        foreach (var (entityId, states) in result.Value)
                            data.HistoryData[entityId] = states;
                    }
                }
            }
        }

        return data;
    }

    // =============================================
    // HELPERS
    // =============================================

    internal static string? GetStringProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static HashSet<string> CollectEntityIds(LayoutConfig layout)
    {
        var ids = new HashSet<string>();
        foreach (var widget in layout.Widgets)
        {
            switch (widget.Type)
            {
                case "calendar":
                case "weather":
                case "weather-forecast":
                case "todo":
                case "rss-feed":
                    AddId(widget.Config, "entityId", ids);
                    break;
                case "graph":
                    if (widget.Config.TryGetProperty("series", out var series)
                        && series.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in series.EnumerateArray())
                            AddId(s, "entityId", ids);
                    }
                    break;
                case "header":
                    if (widget.Config.TryGetProperty("badges", out var badges)
                        && badges.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var badge in badges.EnumerateArray())
                            AddId(badge, "entityId", ids);
                    }
                    break;
            }
        }
        return ids;

        static void AddId(JsonElement el, string prop, HashSet<string> ids)
        {
            var val = el.TryGetProperty(prop, out var p) ? p.GetString() : null;
            if (!string.IsNullOrEmpty(val)) ids.Add(val);
        }
    }
}
