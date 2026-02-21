using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Security.Claims;

namespace EPaperDashboard.Services;

public interface IDeploymentStrategy
{
    DeploymentMode Mode { get; }
    bool IsUserManagementEnabled { get; }
    /// <summary>
    /// When true, the strategy provides HA connection credentials automatically
    /// (e.g. via supervisor token) so dashboards don't need individual OAuth tokens.
    /// </summary>
    bool IsAutoConnected { get; }
    /// <summary>
    /// The WebSocket endpoint path relative to the host URL.
    /// Supervisor proxy uses "/websocket"; direct HA uses "/api/websocket".
    /// </summary>
    string WebSocketPath { get; }
    Task<string?> CreateAccessTokenAsync(string clientName);
    (string host, string token) GetHomeAssistantConnection(Dashboard dashboard);
    UnitResult<string> ValidateConfiguration();
    string GetConfigDirectory();
    ClaimsPrincipal? AuthenticateViaIngress(HttpContext context);
    Uri? GetOAuthClientUri(HttpContext? context = null);
    Task<bool> ProcessIngressPathAsync(HttpContext context, IWebHostEnvironment environment);
    void PerformInitialSetup(IServiceProvider serviceProvider);
    void ApplyMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);
    void ApplyPostAuthenticationMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);
    void ApplyPostStaticFilesMiddleware(IApplicationBuilder app, IWebHostEnvironment environment);
}
