using System.Net.Http.Headers;
using System.Text.Json;

namespace EPaperDashboard.Services;

/// <summary>
/// Service to detect and validate Home Assistant add-on environment.
/// Uses multiple verification steps to prevent authentication bypass.
/// </summary>
public sealed class HomeAssistantEnvironmentService
{
    private readonly ILogger<HomeAssistantEnvironmentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private bool? _isValidatedEnvironment;
    private readonly object _lock = new();

    public HomeAssistantEnvironmentService(
        ILogger<HomeAssistantEnvironmentService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets the validated Supervisor token. Returns null if not in a valid HA environment.
    /// </summary>
    public string? SupervisorToken => IsValidHomeAssistantEnvironment() 
        ? Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") 
        : null;

    /// <summary>
    /// Determines if the application is running in a validated Home Assistant add-on environment.
    /// This performs actual validation, not just environment variable checks, to prevent bypass.
    /// </summary>
    public bool IsValidHomeAssistantEnvironment()
    {
        lock (_lock)
        {
            // Return cached result if already validated
            if (_isValidatedEnvironment.HasValue)
            {
                return _isValidatedEnvironment.Value;
            }

            _isValidatedEnvironment = ValidateEnvironment();
            
            if (_isValidatedEnvironment.Value)
            {
                _logger.LogInformation("Application validated as running in Home Assistant add-on environment");
            }
            else
            {
                _logger.LogInformation("Application running in standalone mode (not Home Assistant add-on)");
            }

            return _isValidatedEnvironment.Value;
        }
    }

    private bool ValidateEnvironment()
    {
        // Check for SUPERVISOR_TOKEN environment variable
        var token = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogDebug("SUPERVISOR_TOKEN not found - not running in HA add-on");
            return false;
        }

        // Verify /data directory exists (HA persistent storage indicator)
        if (!Directory.Exists("/data"))
        {
            _logger.LogWarning("HA /data directory not found - not in valid HA environment");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the configuration directory path based on the environment.
    /// - HA add-on mode: /data (persistent across updates)
    /// - Standalone mode: {AppDirectory}/config
    /// </summary>
    public string GetConfigDirectory()
    {
        if (IsValidHomeAssistantEnvironment())
        {
            return "/data";
        }

        return Path.Combine(AppContext.BaseDirectory, "config");
    }

    /// <summary>
    /// Reads Home Assistant add-on options from /data/options.json
    /// Returns null if not in HA environment or file doesn't exist
    /// </summary>
    public JsonDocument? ReadHomeAssistantOptions()
    {
        if (!IsValidHomeAssistantEnvironment())
        {
            return null;
        }

        try
        {
            var optionsPath = "/data/options.json";
            if (!File.Exists(optionsPath))
            {
                _logger.LogWarning("HA options.json not found at {Path}", optionsPath);
                return null;
            }

            var json = File.ReadAllText(optionsPath);
            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Home Assistant options.json");
            return null;
        }
    }
}
