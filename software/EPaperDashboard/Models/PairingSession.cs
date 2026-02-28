namespace EPaperDashboard.Models;

public enum PairingStatus
{
    Pending = 0,
    AwaitingConfirmation = 1,
    Confirmed = 2,
    Completed = 3
}

public class PairingSession
{
    public PairingSessionId Id { get; set; } = PairingSessionId.Empty;
    public UserId UserId { get; set; } = UserId.Empty;
    public string Code { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ConfirmationPin { get; set; } = string.Empty;
    public PairingStatus Status { get; set; } = PairingStatus.Pending;
    public int FailedAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? DeviceIdentifier { get; set; }

    public int? ScreenWidth { get; set; }

    public int? ScreenHeight { get; set; }
}
