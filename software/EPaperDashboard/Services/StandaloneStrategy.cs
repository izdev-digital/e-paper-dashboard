using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Security.Claims;

namespace EPaperDashboard.Services;

/// <summary>
/// Deployment strategy for standalone mode (non-Home Assistant).
/// Uses OAuth tokens and requires full configuration.
/// </summary>
public class StandaloneStrategy : IDeploymentStrategy
{
    private readonly ILogger<StandaloneStrategy> _logger;

    public StandaloneStrategy(ILogger<StandaloneStrategy> logger)
    {
        _logger = logger;
    }

    public bool IsHomeAssistantAddon => false;

    public bool IsUserManagementEnabled => true;

    public string GetConfigDirectory() => EnvironmentConfiguration.ConfigDir;

    public Task<string?> CreateAccessTokenAsync(string clientName)
    {
        // Long-lived token creation via supervisor is not available in standalone mode
        _logger.LogWarning("Long-lived token creation is not supported in standalone mode");
        return Task.FromResult<string?>(null);
    }

    public (string host, string token) GetHomeAssistantConnection(Dashboard dashboard)
    {
        // Use dashboard's configured OAuth credentials
        return (dashboard.Host!, dashboard.AccessToken!);
    }

    public UnitResult<string> ValidateConfiguration()
    {
        var missingConfigs = new List<string>();

        if (EnvironmentConfiguration.ClientUri is null)
        {
            missingConfigs.Add("CLIENT_URL");
        }

        if (string.IsNullOrWhiteSpace(EnvironmentConfiguration.SuperUserUsername))
        {
            missingConfigs.Add("SUPERUSER_USERNAME");
        }

        if (string.IsNullOrWhiteSpace(EnvironmentConfiguration.SuperUserPassword))
        {
            missingConfigs.Add("SUPERUSER_PASSWORD");
        }

        if (string.IsNullOrWhiteSpace(EnvironmentConfiguration.StateSigningKey))
        {
            missingConfigs.Add("STATE_SIGNING_KEY");
        }

        if (missingConfigs.Count > 0)
        {
            var message = $"""
                Missing required configuration: {string.Join(", ", missingConfigs)}.
                Please set them as environment variables or in /data/options.json file.
                """;
            return UnitResult.Failure(message);
        }

        return UnitResult.Success<string>();
    }

    public ClaimsPrincipal? AuthenticateViaIngress(HttpContext context)
    {
        // Ingress authentication is only available in HA add-on mode
        return null;
    }

    public Task<bool> ProcessIngressPathAsync(HttpContext context, IWebHostEnvironment environment)
    {
        // No ingress path processing in standalone mode
        return Task.FromResult(false);
    }

    public void PerformInitialSetup(IServiceProvider serviceProvider)
    {
        var userService = serviceProvider.GetRequiredService<UserService>();
        if (!userService.HasSuperUser() 
            && EnvironmentConfiguration.SuperUserUsername != null 
            && EnvironmentConfiguration.SuperUserPassword != null)
        {
            userService.TryCreateUser(
                EnvironmentConfiguration.SuperUserUsername, 
                EnvironmentConfiguration.SuperUserPassword, 
                isSuperUser: true);
            
            _logger.LogInformation("Created superuser: {Username}", EnvironmentConfiguration.SuperUserUsername);
        }
    }

    public void ApplyMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
    }

    public void ApplyPostRoutingMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
    }

    public void ApplyPostStaticFilesMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
    }
}
