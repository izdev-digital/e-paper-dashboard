namespace EPaperDashboard.Services.Ai;

/// <summary>
/// Snapshot of data fetched from HA for AI prompt building.
/// Similar to SsrData but without coupling to the rendering pipeline.
/// </summary>
public sealed class AiDataSnapshot
{
    public Dictionary<string, HassEntityState> EntityStates { get; set; } = new();
    public Dictionary<string, List<TodoItem>> TodoItems { get; set; } = new();
    public Dictionary<string, List<CalendarEvent>> CalendarEvents { get; set; } = new();
    public Dictionary<string, List<object?>> WeatherForecasts { get; set; } = new();
    public Dictionary<string, List<RssFeedEntry>> RssFeedEntries { get; set; } = new();
}
