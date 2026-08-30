using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;
using Microsoft.Extensions.Caching.Memory;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Resolves a deduplicated widget data plan into the snapshot shared by native rendering and
/// designer previews. Successful source values are cached briefly; failures are never cached.
/// </summary>
public sealed class SsrDataProvider(
    IEntityStateProvider entityStateProvider,
    ITodoDataProvider todoDataProvider,
    ICalendarDataProvider calendarDataProvider,
    IWeatherForecastProvider weatherForecastProvider,
    IEntityHistoryProvider entityHistoryProvider,
    IAiContentProvider aiContentProvider,
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<SsrDataProvider> logger) : ISsrDataProvider
{
    private static readonly TimeSpan StateCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TodoCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CalendarCacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ForecastCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromMinutes(1);

    public async Task<SsrData> FetchSsrDataAsync(
        string dashboardId,
        LayoutConfig layout,
        CancellationToken cancellationToken = default,
        bool bypassCache = false)
    {
        var data = new SsrData();
        var plan = WidgetDataPlan.Create(layout);

        await FetchEntityStatesAsync(dashboardId, plan, data, bypassCache, cancellationToken);
        ResolveRssEntries(plan, data);

        var tasks = new List<Task>();
        tasks.AddRange(plan.TodoEntityIds.Select(entityId =>
            FetchTodoAsync(dashboardId, entityId, data, bypassCache, cancellationToken)));
        tasks.AddRange(plan.CalendarEntityIds.Select(entityId =>
            FetchCalendarAsync(dashboardId, entityId, data, bypassCache, cancellationToken)));
        tasks.AddRange(plan.Forecasts.Select(request =>
            FetchWeatherAsync(dashboardId, request, data, bypassCache, cancellationToken)));
        tasks.AddRange(plan.HistoryHoursByEntityId
            .GroupBy(item => item.Value)
            .Select(group => FetchGraphHistoryAsync(
                dashboardId,
                group.Select(item => item.Key).OrderBy(id => id, StringComparer.Ordinal).ToList(),
                group.Key,
                data,
                bypassCache,
                cancellationToken)));

        foreach (var widgetId in plan.CachedContentWidgetIds)
            ResolveCachedContent(dashboardId, widgetId, data);

        await Task.WhenAll(tasks);
        return data;
    }

    private async Task FetchEntityStatesAsync(
        string dashboardId,
        WidgetDataPlan plan,
        SsrData data,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        if (plan.EntityStateIds.Count == 0) return;

        var entityIds = plan.EntityStateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var cacheKey = $"source:{dashboardId}:states:{string.Join('|', entityIds)}";
        var (result, fromCache) = await FetchCachedAsync(
            cacheKey,
            StateCacheDuration,
            () => entityStateProvider.FetchEntityStatesAsync(dashboardId, entityIds, cancellationToken),
            bypassCache,
            cancellationToken);

        if (result.IsFailure)
        {
            foreach (var entityId in entityIds)
                SetFailure(data, DataSourceKeys.Entity(entityId), result.Error);
            logger.LogWarning("Failed to fetch entity states: {Error}", result.Error);
            return;
        }

        foreach (var state in result.Value)
            data.EntityStates[state.EntityId] = state;

        foreach (var entityId in entityIds)
        {
            var count = data.EntityStates.ContainsKey(entityId) ? 1 : 0;
            SetSuccess(data, DataSourceKeys.Entity(entityId), count, fromCache);
        }
    }

    private async Task FetchTodoAsync(
        string dashboardId,
        string entityId,
        SsrData data,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var (result, fromCache) = await FetchCachedAsync(
            $"source:{dashboardId}:todo:{entityId}",
            TodoCacheDuration,
            () => todoDataProvider.FetchTodoItemsAsync(dashboardId, entityId, cancellationToken),
            bypassCache,
            cancellationToken);

        if (result.IsSuccess)
        {
            data.TodoItems[entityId] = result.Value;
            SetSuccess(data, DataSourceKeys.Todo(entityId), result.Value.Count, fromCache);
        }
        else
        {
            SetFailure(data, DataSourceKeys.Todo(entityId), result.Error);
        }
    }

    private async Task FetchCalendarAsync(
        string dashboardId,
        string entityId,
        SsrData data,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var (result, fromCache) = await FetchCachedAsync(
            $"source:{dashboardId}:calendar:{entityId}:168",
            CalendarCacheDuration,
            () => calendarDataProvider.FetchCalendarEventsAsync(dashboardId, entityId, 168, cancellationToken),
            bypassCache,
            cancellationToken);

        if (result.IsSuccess)
        {
            data.CalendarEvents[entityId] = result.Value;
            SetSuccess(data, DataSourceKeys.Calendar(entityId), result.Value.Count, fromCache);
        }
        else
        {
            SetFailure(data, DataSourceKeys.Calendar(entityId), result.Error);
        }
    }

    private async Task FetchWeatherAsync(
        string dashboardId,
        WeatherForecastDataKey request,
        SsrData data,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var (result, fromCache) = await FetchCachedAsync(
            $"source:{dashboardId}:forecast:{request.EntityId}:{request.ForecastType}",
            ForecastCacheDuration,
            () => weatherForecastProvider.FetchWeatherForecastAsync(
                dashboardId,
                request.EntityId,
                request.ForecastType,
                cancellationToken),
            bypassCache,
            cancellationToken);

        if (result.IsFailure)
        {
            SetFailure(data, DataSourceKeys.Forecast(request), result.Error);
            return;
        }

        var forecast = result.Value;
        data.WeatherForecasts[request] = forecast;
        SetSuccess(data, DataSourceKeys.Forecast(request), forecast.Count, fromCache);
    }

    private async Task FetchGraphHistoryAsync(
        string dashboardId,
        List<string> entityIds,
        int hours,
        SsrData data,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var (result, fromCache) = await FetchCachedAsync(
            $"source:{dashboardId}:history:{hours}:{string.Join('|', entityIds)}",
            HistoryCacheDuration,
            () => entityHistoryProvider.FetchEntityHistoryAsync(dashboardId, entityIds, hours, cancellationToken),
            bypassCache,
            cancellationToken);

        if (result.IsFailure)
        {
            foreach (var entityId in entityIds)
                SetFailure(data, DataSourceKeys.History(entityId), result.Error);
            return;
        }

        foreach (var entityId in entityIds)
        {
            var states = result.Value.GetValueOrDefault(entityId) ?? [];
            data.HistoryData[entityId] = states;
            SetSuccess(data, DataSourceKeys.History(entityId), states.Count, fromCache);
        }
    }

    private void ResolveRssEntries(WidgetDataPlan plan, SsrData data)
    {
        foreach (var entityId in plan.RssEntityIds)
        {
            if (!data.EntityStates.TryGetValue(entityId, out var state))
            {
                var entityStatus = data.SourceStatuses.GetValueOrDefault(DataSourceKeys.Entity(entityId));
                if (entityStatus?.State == "error")
                    SetFailure(data, DataSourceKeys.Rss(entityId), entityStatus.Error ?? "Entity state unavailable");
                else
                    SetSuccess(data, DataSourceKeys.Rss(entityId), 0, entityStatus?.FromCache == true);
                data.RssFeedEntries[entityId] = [];
                continue;
            }

            var title = GetAttributeString(state, "title");
            var link = GetAttributeString(state, "link");
            var entries = string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(link)
                ? []
                : new List<RssFeedEntry>
                {
                    new()
                    {
                        Title = title ?? string.Empty,
                        Link = link ?? string.Empty,
                        Published = GetAttributeString(state, "published"),
                        Summary = GetAttributeString(state, "description")
                            ?? GetAttributeString(state, "summary")
                            ?? GetAttributeString(state, "content")
                    }
                };
            data.RssFeedEntries[entityId] = entries;
            var fromCache = data.SourceStatuses.GetValueOrDefault(DataSourceKeys.Entity(entityId))?.FromCache == true;
            SetSuccess(data, DataSourceKeys.Rss(entityId), entries.Count, fromCache);
        }
    }

    private void ResolveCachedContent(string dashboardId, string widgetId, SsrData data)
    {
        var content = aiContentProvider.GetCachedContent(dashboardId, widgetId);
        if (!string.IsNullOrEmpty(content)) data.AiContent[widgetId] = content;
        SetSuccess(data, DataSourceKeys.Generated(widgetId), string.IsNullOrEmpty(content) ? 0 : 1, false);
    }

    private async Task<(Result<T, string> Result, bool FromCache)> FetchCachedAsync<T>(
        string cacheKey,
        TimeSpan duration,
        Func<Task<Result<T, string>>> fetch,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!bypassCache && cache.TryGetValue<T>(cacheKey, out var cached) && cached is not null)
            return (Result.Success<T, string>(cached), true);

        var result = await fetch().WaitAsync(cancellationToken);
        if (result.IsSuccess) cache.Set(cacheKey, result.Value, duration);
        return (result, false);
    }

    private void SetSuccess(SsrData data, string key, int itemCount, bool fromCache) =>
        data.SourceStatuses[key] = DataSourceStatus.Success(itemCount, timeProvider.GetUtcNow(), fromCache);

    private void SetFailure(SsrData data, string key, string error) =>
        data.SourceStatuses[key] = DataSourceStatus.Failed(error, timeProvider.GetUtcNow());

    private static string? GetAttributeString(HassEntityState state, string name) =>
        state.Attributes.TryGetValue(name, out var value) ? value?.ToString() : null;
}
