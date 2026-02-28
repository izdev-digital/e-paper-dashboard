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
    }
}
