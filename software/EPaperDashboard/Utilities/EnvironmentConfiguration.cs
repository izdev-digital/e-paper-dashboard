namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private static readonly Lazy<Uri?> _rendererUri = new(() => Environment
		.GetEnvironmentVariable("RENDERER_URL") is { } uriString ? new Uri(uriString) : null);

	private static readonly Lazy<Uri?> _dashboardUri = new(() => Environment
		.GetEnvironmentVariable("DASHBOARD_URL") is { } uriString ? new Uri(uriString) : null);

	public static Uri RendererUri => _rendererUri.Value ?? throw new ArgumentException("Renderer url is not specified");

	public static Uri DashboardUri => _dashboardUri.Value ?? throw new ArgumentException("Dashboard url is not specified");
}
