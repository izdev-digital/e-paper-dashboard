using EPaperDashboard.Guards;

namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private static readonly Lazy<string?> _hassToken = new(() => Environment
		.GetEnvironmentVariable("HASS_TOKEN"));

	private static readonly Lazy<Uri?> _hassUri = new(() =>
		GetUriFromEnvironment("HASS_URL", UriKind.Absolute));

	private static readonly Lazy<Uri?> _dashboardPath = new(() =>
		GetUriFromEnvironment("DASHBOARD_PATH", UriKind.Relative));

	private static readonly Lazy<Uri?> _clientUri = new(() =>
		GetUriFromEnvironment("CLIENT_URL", UriKind.Absolute));

	private static readonly Lazy<string?> _superuserUsername = new(() => Environment
		.GetEnvironmentVariable("SUPERUSER_USERNAME"));

	private static readonly Lazy<string?> _superuserPassword = new(() => Environment
		.GetEnvironmentVariable("SUPERUSER_PASSWORD"));

	public static string HassToken => Guard.NeitherNullNorWhitespace(_hassToken.Value);

	public static Uri HassUri => Guard.NotNull(_hassUri.Value);

	public static Uri DashboardUri => new(HassUri, HassDashboardPath);

	public static Uri ClientUri => Guard.NotNull(_clientUri.Value);

	public static string SuperUserUsername => _superuserUsername.Value ?? "admin";
	public static string SuperUserPassword => _superuserPassword.Value ?? "admin";

	private static Uri HassDashboardPath => Guard.NotNull(_dashboardPath.Value);
	
	private static Uri? GetUriFromEnvironment(string variable, UriKind kind) => Environment
		.GetEnvironmentVariable(variable) is { } uriString ? new Uri(uriString, kind) : null;
}
