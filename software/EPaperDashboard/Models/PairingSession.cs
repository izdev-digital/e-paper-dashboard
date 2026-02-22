namespace EPaperDashboard.Models;

public class PairingSession
{
    public PairingSessionId Id { get; set; } = PairingSessionId.Empty;
    public DashboardId DashboardId { get; set; } = DashboardId.Empty;
    public string Code { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? DeviceIdentifier { get; set; }
}
