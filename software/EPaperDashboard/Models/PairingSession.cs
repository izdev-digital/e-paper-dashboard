namespace EPaperDashboard.Models;

public class PairingSession
{
    public Guid Id { get; set; } = Guid.Empty;
    public Guid DashboardId { get; set; } = Guid.Empty;
    public string Code { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? DeviceIdentifier { get; set; }
}
