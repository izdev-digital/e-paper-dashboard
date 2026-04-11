using CSharpFunctionalExtensions;
using EPaperDashboard.Services.Providers;

namespace EPaperDashboard.Services.Ai;

public sealed class AiDataFetcher(
    IEntityStateProvider entityStateProvider,
    ITodoDataProvider todoDataProvider,
    ICalendarDataProvider calendarDataProvider,
    IWeatherForecastProvider weatherForecastProvider,
    IRssFeedDataProvider rssFeedDataProvider,
    ILogger<AiDataFetcher> logger)
{
    public async Task<AiDataSnapshot> FetchAsync(string dashboardId)
    {
        var data = new AiDataSnapshot();

        var entityStatesTask = SafeFetchAsync(() => entityStateProvider.FetchAllEntityStatesAsync(dashboardId));
        var todoTask = SafeFetchAsync(() => todoDataProvider.FetchAllTodoItemsAsync(dashboardId));
        var calendarTask = SafeFetchAsync(() => calendarDataProvider.FetchAllCalendarEventsAsync(dashboardId));
        var weatherTask = SafeFetchAsync(() => weatherForecastProvider.FetchAllWeatherForecastsAsync(dashboardId));
        var rssTask = SafeFetchAsync(() => rssFeedDataProvider.FetchAllRssFeedEntriesAsync(dashboardId));

        await Task.WhenAll(entityStatesTask, todoTask, calendarTask, weatherTask, rssTask);

        var entityStates = await entityStatesTask;
        if (entityStates != null)
        {
            foreach (var state in entityStates)
            {
                data.EntityStates[state.EntityId] = state;
            }
        }

        var todoItems = await todoTask;
        if (todoItems != null)
        {
            data.TodoItems = todoItems;
        }

        var calendarEvents = await calendarTask;
        if (calendarEvents != null)
        {
            data.CalendarEvents = calendarEvents;
        }

        var weatherForecasts = await weatherTask;
        if (weatherForecasts != null)
        {
            data.WeatherForecasts = weatherForecasts;
        }

        var rssEntries = await rssTask;
        if (rssEntries != null)
        {
            data.RssFeedEntries = rssEntries;
        }

        logger.LogInformation(
            "AI data snapshot for dashboard {DashboardId}: {States} entity states, {Todo} todo lists, {Cal} calendars, {Weather} weather entities, {Rss} RSS feeds",
            dashboardId, data.EntityStates.Count, data.TodoItems.Count,
            data.CalendarEvents.Count, data.WeatherForecasts.Count, data.RssFeedEntries.Count);

        return data;
    }

    private async Task<T?> SafeFetchAsync<T>(Func<Task<Result<T, string>>> fetch) where T : class
    {
        try
        {
            var result = await fetch();
            if (result.IsFailure)
            {
                logger.LogWarning("Provider fetch failed: {Error}", result.Error);
                return null;
            }
            return result.Value;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            logger.LogWarning("Provider fetch timed out: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provider fetch failed: {Message}", ex.Message);
            return null;
        }
    }
}
