using LiteDB;

namespace EPaperDashboard.Models
{
    public enum RenderingMode
    {
        Custom = 0,
        HomeAssistant = 1
    }

    public class Dashboard
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public ObjectId UserId { get; set; } = ObjectId.Empty;
        public string? AccessToken { get; set; }
        public string? Host { get; set; }
        public string? Path { get; set; }
        public List<TimeOnly>? UpdateTimes { get; set; }
        public LayoutConfig? LayoutConfig { get; set; }
        public DateTimeOffset? LastUpdateTime { get; set; }
        public RenderingMode RenderingMode { get; set; } = RenderingMode.Custom;
    }
}
