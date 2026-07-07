using EPaperDashboard.Models;

namespace EPaperDashboard.Services;

/// <summary>
/// Decides whether a device's firmware version / last-seen timestamp needs updating on a device-port
/// request, and applies that update. A device is refreshed if its reported firmware version changed,
/// it has never been seen before, or more than a minute has passed since its last recorded check-in
/// (avoids writing to the repository on every single request from an active device).
/// </summary>
public sealed class DeviceLastSeenTracker(TimeProvider timeProvider)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    public bool ShouldUpdate(Device device, string incomingFirmwareVersion) =>
        device.FirmwareVersion != incomingFirmwareVersion
        || device.LastSeenAt is null
        || timeProvider.GetUtcNow() - device.LastSeenAt > StaleAfter;

    public void Apply(Device device, string firmwareVersion)
    {
        device.FirmwareVersion = firmwareVersion;
        device.LastSeenAt = timeProvider.GetUtcNow();
    }
}
