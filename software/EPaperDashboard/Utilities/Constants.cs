
namespace EPaperDashboard.Utilities;

public static class Constants
{
    public const string AppName = "izBoard";

    public const string CompanyName = "izdev.digital";

    public static string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        
        return version?.ToString() ?? "0.0.0";
    }

    public const string DashboardHttpClientName = nameof(DashboardHttpClientName);

    public const string HassHttpClientName = nameof(HassHttpClientName);

    // Home Assistant Constants
    public const string SupervisorCoreUrl = "http://supervisor/core";
    public const string IngressPathHeader = "X-Ingress-Path";
    
    // Claim Types
    public const string IsSuperUserClaim = "IsSuperUser";
    public const string HomeAssistantIngressClaim = "HomeAssistantIngress";
    public const string HomeAssistantAdminUserId = "ha-admin";
    public const string HomeAssistantAdminUserName = "Home Assistant Admin";
    
    // Home Assistant virtual user ObjectId (deterministic value for HA mode dashboards)
    public static readonly LiteDB.ObjectId HomeAssistantVirtualUserId = new LiteDB.ObjectId("000000000000000000000001");
}
