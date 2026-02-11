using EPaperDashboard.Models;
using CSharpFunctionalExtensions;
using System.Security.Claims;

namespace EPaperDashboard.Services;

/// <summary>
/// Abstraction for different deployment modes (Home Assistant add-on vs standalone).
/// </summary>
public interface IDeploymentStrategy
{
    /// <summary>
    /// Indicates whether running in Home Assistant add-on mode.
    /// </summary>
    bool IsHomeAssistantAddon { get; }

    /// <summary>
    /// Creates a long-lived access token for dashboard authentication.
    /// Returns null if not supported in this deployment mode.
    /// </summary>
    Task<string?> CreateAccessTokenAsync(string clientName);

    /// <summary>
    /// Gets the Home Assistant connection details (host and token) for API access.
    /// In HA add-on mode, uses supervisor endpoint and token.
    /// In standalone mode, uses dashboard's configured host and OAuth token.
    /// </summary>
    (string host, string token) GetHomeAssistantConnection(Dashboard dashboard);

    /// <summary>
    /// Validates that required configuration is present for this deployment mode.
    /// </summary>
    UnitResult<string> ValidateConfiguration();

    /// <summary>
    /// Gets the configuration directory path for this deployment mode.
    /// </summary>
    string GetConfigDirectory();

    /// <summary>
    /// Determines whether user management endpoints should be available.
    /// In HA add-on mode with ingress, user management is disabled.
    /// </summary>
    bool IsUserManagementEnabled { get; }

    /// <summary>
    /// Attempts to authenticate a request via ingress headers (HA add-on mode).
    /// Returns claims principal if authenticated via ingress, null otherwise.
    /// </summary>
    ClaimsPrincipal? AuthenticateViaIngress(HttpContext context);

    /// <summary>
    /// Processes ingress path for request rewriting (HA add-on mode).
    /// Returns true if path was processed and response was written.
    /// </summary>
    Task<bool> ProcessIngressPathAsync(HttpContext context, IWebHostEnvironment environment);

    /// <summary>
    /// Performs initial setup operations for this deployment mode.
    /// In standalone mode, ensures superuser exists.
    /// In HA add-on mode, no setup needed.
    /// </summary>
    void PerformInitialSetup(IServiceProvider serviceProvider);

    /// <summary>
    /// Applies deployment-specific middleware to the application pipeline.
    /// </summary>
    void ApplyMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);

    /// <summary>
    /// Applies deployment-specific middleware after authentication.
    /// Used for ingress-based authentication in HA add-on mode.
    /// </summary>
    void ApplyPostAuthenticationMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);

    /// <summary>
    /// Applies deployment-specific middleware after static files.
    /// </summary>
    void ApplyPostStaticFilesMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);
}
