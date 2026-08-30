using EPaperDashboard.Services;

namespace EPaperDashboard.Models.Rendering;

public sealed record DashboardPreviewData(
    IReadOnlyDictionary<string, HassEntityState> EntityStates,
    IReadOnlyDictionary<string, List<TodoItem>> TodoItems,
    IReadOnlyDictionary<string, List<CalendarEvent>> CalendarEvents,
    IReadOnlyDictionary<string, List<object?>> WeatherForecasts,
    IReadOnlyDictionary<string, List<RssFeedEntry>> RssFeedEntries,
    IReadOnlyDictionary<string, List<HistoryState>> HistoryData,
    IReadOnlyDictionary<string, string> GeneratedContent,
    string AppVersion,
    DateTimeOffset FetchedAt)
{
    public static DashboardPreviewData FromSsrData(SsrData data, TimeProvider timeProvider) => new(
        data.EntityStates,
        data.TodoItems,
        data.CalendarEvents,
        data.WeatherForecasts.ToDictionary(
            item => $"{item.Key.EntityId}\u0000{item.Key.ForecastType}",
            item => item.Value),
        data.RssFeedEntries,
        data.HistoryData,
        data.AiContent,
        typeof(Services.Rendering.DashboardImageRenderingService).Assembly.GetName().Version?.ToString() ?? "?",
        timeProvider.GetUtcNow());
}
