using System.Text.Json;
using EPaperDashboard.Guards;

namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private static readonly Lazy<JsonDocument?> _jsonConfig = new(LoadJsonConfig);

	private static readonly Lazy<Uri?> _clientUri = new(() =>
		GetUriFromEnvOrConfig("CLIENT_URL", UriKind.Absolute));

	private static readonly Lazy<string?> _superuserUsername = new(() =>
		GetStringFromEnvOrConfig("SUPERUSER_USERNAME"));

	private static readonly Lazy<string?> _superuserPassword = new(() =>
		GetStringFromEnvOrConfig("SUPERUSER_PASSWORD"));

	private static readonly Lazy<string> _configDir = new(() =>
		Path.Combine(AppContext.BaseDirectory, "config"));

	public static Uri ClientUri => Guard.NotNull(_clientUri.Value);

	public static string SuperUserUsername => _superuserUsername.Value ?? "admin";

	public static string SuperUserPassword => _superuserPassword.Value ?? "admin";

	public static string ConfigDir => _configDir.Value;

	private static JsonDocument? LoadJsonConfig()
	{
		try
		{
			var configFile = Path.Combine(ConfigDir, "environment.json");
			if (!File.Exists(configFile))
			{
				return null;
			}

			var json = File.ReadAllText(configFile);
			return JsonDocument.Parse(json);
		}
		catch
		{
			return null;
		}
	}

	private static string? GetStringFromEnvOrConfig(string key)
	{
		var env = Environment.GetEnvironmentVariable(key);
		if (!string.IsNullOrWhiteSpace(env))
		{
			return env;
		}

		var doc = _jsonConfig.Value;
		if (doc is null)
		{
			return null;
		}

		if (!doc.RootElement.TryGetProperty(key, out var el))
		{
			return null;
		}

		return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
	}

	private static Uri? GetUriFromEnvOrConfig(string variable, UriKind kind)
	{
		var value = GetStringFromEnvOrConfig(variable);
		return !string.IsNullOrWhiteSpace(value) ? new Uri(value, kind) : null;
	}
}
