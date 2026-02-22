namespace EPaperDashboard.Models
{
    public class User
    {
        public UserId Id { get; set; } = UserId.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsSuperUser { get; set; }
        public string? Nickname { get; set; }
    }
}
