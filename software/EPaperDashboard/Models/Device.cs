namespace EPaperDashboard.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.Empty;
    public Guid DashboardId { get; set; } = Guid.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? FirmwareVersion { get; set; }
}
