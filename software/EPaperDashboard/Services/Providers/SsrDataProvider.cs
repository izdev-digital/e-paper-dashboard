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
    IAiContentProvider aiContentProvider,
    ILogger<SsrDataProvider> logger) : ISsrDataProvider
{
    private readonly IEntityStateProvider _entityStateProvider = entityStateProvider;
    private readonly ITodoDataProvider _todoDataProvider = todoDataProvider;
    private readonly ICalendarDataProvider _calendarDataProvider = calendarDataProvider;
    private readonly IWeatherForecastProvider _weatherForecastProvider = weatherForecastProvider;
    private readonly IRssFeedDataProvider _rssFeedDataProvider = rssFeedDataProvider;
    private readonly IEntityHistoryProvider _entityHistoryProvider = entityHistoryProvider;
    private readonly IAiContentProvider _aiContentProvider = aiContentProvider;
    private readonly ILogger<SsrDataProvider> _logger = logger;

    public async Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout, CancellationToken cancellationToken = default)
    {
        var data = new SsrData();

        // Collect all entity IDs needed across all widgets
        var entityIds = CollectEntityIds(layout);

        // Fetch all entity states first — other providers may depend on them
        if (entityIds.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

        // Fetch remaining data sources in parallel
        var tasks = new List<Task>();

        // Single-pass grouping instead of scanning widgets 6 times
        var widgetsByType = layout.Widgets
            .GroupBy(w => w.Type)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (widgetsByType.TryGetValue("todo", out var todoWidgets))
        {
            foreach (var entityId in todoWidgets
                .Select(widget => GetStringProp(widget.Config, "entityId"))
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal))
            {
                tasks.Add(FetchTodoAsync(dashboardId, entityId, data));
            }
        }

        if (widgetsByType.TryGetValue("calendar", out var calendarWidgets))
        {
            foreach (var entityId in calendarWidgets
                .Select(widget => GetStringProp(widget.Config, "entityId"))
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal))
            {
                tasks.Add(FetchCalendarAsync(dashboardId, entityId, data));
            }
        }

        if (widgetsByType.TryGetValue("weather-forecast", out var forecastWidgets))
        {
            var requests = forecastWidgets
                .Select(widget => new
                {
                    EntityId = GetStringProp(widget.Config, "entityId"),
                    ForecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily"
                })
                .Where(request => !string.IsNullOrEmpty(request.EntityId))
                .Select(request => WeatherForecastDataKey.Create(request.EntityId!, request.ForecastMode))
                .Distinct();

            foreach (var request in requests)
            {
                tasks.Add(FetchWeatherAsync(dashboardId, request, data));
            }
        }

        if (widgetsByType.TryGetValue("rss-feed", out var rssWidgets))
        {
            foreach (var entityId in rssWidgets
                .Select(widget => GetStringProp(widget.Config, "entityId"))
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal))
            {
                tasks.Add(FetchRssAsync(dashboardId, entityId, data));
            }
        }

        if (widgetsByType.TryGetValue("graph", out var graphWidgets))
        {
            var hoursByEntityId = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var widget in graphWidgets)
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

                        foreach (var entityId in graphEntityIds.Distinct(StringComparer.Ordinal))
                        {
                            hoursByEntityId[entityId] = Math.Max(
                                hours,
                                hoursByEntityId.GetValueOrDefault(entityId));
                        }
                    }
                }
            }

            foreach (var group in hoursByEntityId.GroupBy(entry => entry.Value))
            {
                tasks.Add(FetchGraphHistoryAsync(
                    dashboardId,
                    group.Select(entry => entry.Key).ToList(),
                    group.Key,
                    data));
            }
        }

        if (widgetsByType.TryGetValue("ai-content", out var aiWidgets))
        {
            foreach (var widget in aiWidgets)
            {
                var prompt = GetStringProp(widget.Config, "prompt");
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    var widgetId = widget.Id;
                    tasks.Add(FetchAiContentAsync(dashboardId, widgetId, prompt, data));
                }
            }
        }

        await Task.WhenAll(tasks);

        return data;
    }

    private async Task FetchTodoAsync(string dashboardId, string entityId, SsrData data)
    {
        var result = await _todoDataProvider.FetchTodoItemsAsync(dashboardId, entityId);
        if (result.IsSuccess)
            data.TodoItems[entityId] = result.Value;
    }

    private async Task FetchCalendarAsync(string dashboardId, string entityId, SsrData data)
    {
        var result = await _calendarDataProvider.FetchCalendarEventsAsync(dashboardId, entityId, 168);
        if (result.IsSuccess)
            data.CalendarEvents[entityId] = result.Value;
    }

    private async Task FetchWeatherAsync(string dashboardId, WeatherForecastDataKey request, SsrData data)
    {
        var result = await _weatherForecastProvider.FetchWeatherForecastAsync(
            dashboardId,
            request.EntityId,
            request.ForecastType);
        if (result.IsSuccess
            && result.Value.TryGetValue("forecast", out var forecastVal)
            && forecastVal is List<object?> forecastList)
        {
            data.WeatherForecasts[request] = forecastList;
        }
    }

    private async Task FetchRssAsync(string dashboardId, string entityId, SsrData data)
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

    private async Task FetchGraphHistoryAsync(string dashboardId, List<string> entityIds, int hours, SsrData data)
    {
        var result = await _entityHistoryProvider.FetchEntityHistoryAsync(dashboardId, entityIds, hours);
        if (result.IsSuccess)
        {
            foreach (var (entityId, states) in result.Value)
                data.HistoryData[entityId] = states;
        }
    }

    private async Task FetchAiContentAsync(string dashboardId, string widgetId, string prompt, SsrData data)
    {
        // Use cached content if available; fall back to live generation
        var cached = _aiContentProvider.GetCachedContent(dashboardId, widgetId);
        if (cached != null)
        {
            data.AiContent[widgetId] = cached;
            _logger.LogDebug("SSR: Using cached AI content for widget {WidgetId}", widgetId);
            return;
        }

        var result = await _aiContentProvider.GenerateAndCacheContentAsync(dashboardId, widgetId, prompt);
        if (result.IsSuccess)
        {
            data.AiContent[widgetId] = result.Value;
        }
        else
        {
            _logger.LogWarning("SSR: Failed to generate AI content for widget {WidgetId}: {Error}", widgetId, result.Error);
        }
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
