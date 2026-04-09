using System.Text.Json.Serialization;

namespace EPaperDashboard.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RenderingMode
    {
        Custom = 0,
        HomeAssistant = 1
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DashboardOrientation
    {
        Landscape = 0,
        Portrait = 1
    }

    public class Dashboard
    {
        public DashboardId Id { get; set; } = DashboardId.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public UserId UserId { get; set; } = UserId.Empty;
        public string? AccessToken { get; set; }
        public string? Host { get; set; }
        public string? Path { get; set; }
        public List<TimeOnly>? UpdateTimes { get; set; }
        public LayoutConfig? LayoutConfig { get; set; }
        public DateTimeOffset? LastUpdateTime { get; set; }
        public RenderingMode RenderingMode { get; set; } = RenderingMode.Custom;
        public DashboardOrientation Orientation { get; set; } = DashboardOrientation.Landscape;

        public int ScreenWidth { get; set; } = DashboardSizePreset.Default.Width;

        public int ScreenHeight { get; set; } = DashboardSizePreset.Default.Height;

        // AI dashboard generation
        public bool IsAiEnabled { get; set; }
        public string? AiPrompt { get; set; }
        public List<string>? AiDataSourceEntityIds { get; set; }
        public int AiLeadTimeMinutes { get; set; } = 5;
        public List<WidgetConfig>? AiGeneratedWidgets { get; set; }
        public DateTimeOffset? LastAiGenerationTime { get; set; }

        public (int Width, int Height) GetEffectiveSize()
        {
            var w = ScreenWidth > 0 ? ScreenWidth : DashboardSizePreset.Default.Width;
            var h = ScreenHeight > 0 ? ScreenHeight : DashboardSizePreset.Default.Height;
            return Orientation == DashboardOrientation.Portrait
                ? (h, w)
                : (w, h);
        }
    }
}
