namespace EPaperDashboard.Guards;

/// <summary>
/// Marks an endpoint as accessible via the device port (e.g. 8129).
/// Endpoints without this attribute will return 404 on the device port.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DeviceAccessibleAttribute : Attribute
{
    /// <summary>
    /// When true, the device port middleware will check for active pairing sessions
    /// and return 503 if none exist.
    /// </summary>
    public bool RequireActivePairing { get; set; }
}
