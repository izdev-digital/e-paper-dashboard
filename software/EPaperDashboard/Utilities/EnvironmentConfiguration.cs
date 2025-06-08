using EPaperDashboard.Guards;

namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private static readonly Lazy<string?> _hassToken = new(() => Environment.GetEnvironmentVariable("HASS_TOKEN"));

	private static readonly Lazy<Uri?> _hassUri = new(() => Environment
		.GetEnvironmentVariable("HASS_URL") is { } uriString ? new Uri(uriString, UriKind.Absolute) : null);

	private static readonly Lazy<Uri?> _dashboardPath = new(() => Environment
		.GetEnvironmentVariable("DASHBOARD_URL_PATH") is { } uriString ? new Uri(uriString, UriKind.Relative) : null);

	public static string HassToken => Guard.NeitherNullNorWhitespace(_hassToken.Value);

	public static Uri HassUri => Guard.NotNull(_hassUri.Value);

	public static Uri DashboardUri => new Uri(HassUri, HassDashboardPath);

	private static Uri HassDashboardPath => Guard.NotNull(_dashboardPath.Value);
}
