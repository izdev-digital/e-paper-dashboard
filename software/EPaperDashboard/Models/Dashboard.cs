using LiteDB;

namespace EPaperDashboard.Models
{
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
        public DateTimeOffset? LastUpdateTime { get; set; }
    }
}
