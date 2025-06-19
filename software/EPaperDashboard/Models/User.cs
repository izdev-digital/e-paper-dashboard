using LiteDB;

namespace EPaperDashboard.Models
{
    public class User
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsSuperUser { get; set; }
        public string? Nickname { get; set; }
    }
}
