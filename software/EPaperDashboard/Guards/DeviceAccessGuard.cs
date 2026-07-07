using Microsoft.AspNetCore.Http;

namespace EPaperDashboard.Guards;

/// <summary>
/// Decides whether an endpoint is reachable via the device port (e.g. 8129) and whether it
/// requires an active pairing session, based on the <see cref="DeviceAccessibleAttribute"/> metadata.
/// </summary>
public static class DeviceAccessGuard
{
    public static bool IsAccessible(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<DeviceAccessibleAttribute>() is not null;

    public static bool RequiresActivePairing(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<DeviceAccessibleAttribute>()?.RequireActivePairing ?? false;
}
