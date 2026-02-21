using LiteDB;

namespace EPaperDashboard.Models;

public class Device
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;
    public ObjectId DashboardId { get; set; } = ObjectId.Empty;
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? FirmwareVersion { get; set; }
}
