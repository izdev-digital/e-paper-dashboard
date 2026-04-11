using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Ai;

public sealed class AiGenerationResult
{
    public required List<WidgetConfig> Widgets { get; init; }
    public required AiDataSummary DataSummary { get; init; }
    public int PromptTokenEstimate { get; init; }
}

public sealed class AiDataSummary
{
    public int EntityStates { get; init; }
    public List<string> TodoLists { get; init; } = [];
    public List<string> Calendars { get; init; } = [];
    public List<string> WeatherEntities { get; init; } = [];
    public List<string> RssFeeds { get; init; } = [];
}
