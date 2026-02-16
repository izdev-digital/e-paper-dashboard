using LiteDB;

namespace EPaperDashboard.Models;

public class PairingSession
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.Empty;
    public ObjectId DashboardId { get; set; } = ObjectId.Empty;
    public string Code { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? DeviceIdentifier { get; set; }
}
