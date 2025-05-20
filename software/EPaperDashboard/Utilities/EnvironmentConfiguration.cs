namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	public static readonly Lazy<Uri?> RendererUri = new(() => Environment
		.GetEnvironmentVariable("RENDERER_URL") is { } uriString ? new Uri(uriString) : null);

	public static readonly Lazy<Uri?> DashboardUri = new(() => Environment
		.GetEnvironmentVariable("DASHBOARD_URL") is { } uriString ? new Uri(uriString) : null);
}
