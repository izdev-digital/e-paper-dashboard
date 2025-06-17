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
    }
}
