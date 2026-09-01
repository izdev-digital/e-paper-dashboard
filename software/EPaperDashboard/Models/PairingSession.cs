namespace EPaperDashboard.Models;

public enum PairingStatus
{
    Pending = 0,
    Completed = 1,
    Claimed = 2
}

public class PairingSession
{
    public PairingSessionId Id { get; set; } = PairingSessionId.Empty;
    public UserId UserId { get; set; } = UserId.Empty;
    public string Code { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public PairingStatus Status { get; set; } = PairingStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? DeviceIdentifier { get; set; }
    public string? DeviceName { get; set; }
    public string? RegistrationTokenHash { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DeviceId PendingDeviceId { get; set; } = DeviceId.Empty;
    public int? ScreenWidth { get; set; }
    public int? ScreenHeight { get; set; }
}
