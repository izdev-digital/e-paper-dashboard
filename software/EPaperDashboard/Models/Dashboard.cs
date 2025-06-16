namespace EPaperDashboard.Models
{
    public class Dashboard
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
