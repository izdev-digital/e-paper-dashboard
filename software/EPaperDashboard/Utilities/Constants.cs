
namespace EPaperDashboard.Utilities;

public static class Constants
{
    public const string AppName = "E-Paper Dashboard";

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

}
