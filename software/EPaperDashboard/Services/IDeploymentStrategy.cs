using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Security.Claims;

namespace EPaperDashboard.Services;

public interface IDeploymentStrategy
{
    DeploymentMode Mode { get; }
    bool IsUserManagementEnabled { get; }
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
