namespace EPaperDashboard.Services.Ai;

public sealed class AiDataSnapshot
{
    public Dictionary<string, HassEntityState> EntityStates { get; set; } = new();
    public Dictionary<string, List<TodoItem>> TodoItems { get; set; } = new();
    public Dictionary<string, List<CalendarEvent>> CalendarEvents { get; set; } = new();
    public Dictionary<string, List<object?>> WeatherForecasts { get; set; } = new();
    public Dictionary<string, List<RssFeedEntry>> RssFeedEntries { get; set; } = new();
}
