using EPaperDashboard.Guards;

namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private static readonly Lazy<Uri?> _clientUri = new(() =>
		GetUriFromEnvironment("CLIENT_URL", UriKind.Absolute));

	private static readonly Lazy<string?> _superuserUsername = new(() => Environment
		.GetEnvironmentVariable("SUPERUSER_USERNAME"));

	private static readonly Lazy<string?> _superuserPassword = new(() => Environment
		.GetEnvironmentVariable("SUPERUSER_PASSWORD"));

	private static readonly Lazy<string> _configDir = new(() =>
        Path.Combine(AppContext.BaseDirectory, "config"));

	public static Uri ClientUri => Guard.NotNull(_clientUri.Value);

	public static string SuperUserUsername => _superuserUsername.Value ?? "admin";
	
	public static string SuperUserPassword => _superuserPassword.Value ?? "admin";

	public static string ConfigDir => _configDir.Value;

	private static Uri? GetUriFromEnvironment(string variable, UriKind kind) => Environment
		.GetEnvironmentVariable(variable) is { } uriString ? new Uri(uriString, kind) : null;
}
