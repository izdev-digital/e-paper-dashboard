using System.Text.Json;

namespace EPaperDashboard.Utilities;

public static class EnvironmentConfiguration
{
	private const string ClientUrlKey = "CLIENT_URL";
	private const string SuperuserUsernameKey = "SUPERUSER_USERNAME";
	private const string SuperuserPasswordKey = "SUPERUSER_PASSWORD";
	private const string StateSigningKeyKey = "STATE_SIGNING_KEY";
	private const string DashboardScheduleCheckIntervalMinutesKey = "DASHBOARD_SCHEDULE_CHECK_INTERVAL_MINUTES";
	private const string DashboardMissedScheduleToleranceMinutesKey = "DASHBOARD_MISSED_SCHEDULE_TOLERANCE_MINUTES";

	private static readonly Lazy<JsonDocument?> _jsonConfig = new(LoadJsonConfig);

	private static readonly Lazy<Uri?> _clientUri = new(() =>
		GetUriFromEnvOrConfig(ClientUrlKey, UriKind.Absolute));

	private static readonly Lazy<string?> _superuserUsername = new(() =>
		GetStringFromEnvOrConfig(SuperuserUsernameKey));

	private static readonly Lazy<string?> _superuserPassword = new(() =>
		GetStringFromEnvOrConfig(SuperuserPasswordKey));

	private static readonly Lazy<string?> _stateSigningKey = new(() =>
		GetStringFromEnvOrConfig(StateSigningKeyKey));

	private static readonly Lazy<TimeSpan> _dashboardScheduleCheckInterval = new(() =>
		TimeSpan.FromMinutes(GetIntFromEnvOrConfig(DashboardScheduleCheckIntervalMinutesKey, 720))); // 12 hours default

	private static readonly Lazy<TimeSpan> _dashboardMissedScheduleTolerance = new(() =>
		TimeSpan.FromMinutes(GetIntFromEnvOrConfig(DashboardMissedScheduleToleranceMinutesKey, 15))); // 15 minutes default

	private static readonly Lazy<string> _configDir = new(() => "/data");

	private static readonly Lazy<bool> _isHomeAssistantAddon = new(() =>
		!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN"))
		&& Directory.Exists("/data"));

	public static bool IsHomeAssistantAddon => _isHomeAssistantAddon.Value;

	public static Uri? ClientUri => _clientUri.Value;

	public static string? SuperUserUsername => _superuserUsername.Value;

	public static string? SuperUserPassword => _superuserPassword.Value;

	public static string? StateSigningKey => _stateSigningKey.Value;

	public static TimeSpan DashboardScheduleCheckInterval => _dashboardScheduleCheckInterval.Value;

	public static TimeSpan DashboardMissedScheduleTolerance => _dashboardMissedScheduleTolerance.Value;

	public static string ConfigDir => _configDir.Value;

	public static string DataProtectionKeysDir => Path.Combine(ConfigDir, "DataProtection-Keys");

	private static JsonDocument? LoadJsonConfig()
	{
		try
		{
			var optionsFile = Path.Combine(ConfigDir, "options.json");
			if (!File.Exists(optionsFile))
			{
				return null;
			}

			var json = File.ReadAllText(optionsFile);
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

	private static int GetIntFromEnvOrConfig(string key, int defaultValue)
	{
		var stringValue = GetStringFromEnvOrConfig(key);
		return int.TryParse(stringValue, out var intValue) ? intValue : defaultValue;
	}
}
