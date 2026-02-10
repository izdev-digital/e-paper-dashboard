using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EPaperDashboard.Services;

/// <summary>
/// Deployment strategy for Home Assistant add-on mode.
/// Uses supervisor token for authentication and internal networking.
/// </summary>
public class HomeAssistantAddonStrategy : IDeploymentStrategy
{
    private readonly ILogger<HomeAssistantAddonStrategy> _logger;
    private readonly string _supervisorToken;

    public HomeAssistantAddonStrategy(
        ILogger<HomeAssistantAddonStrategy> logger)
    {
        _logger = logger;
        _supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN") 
            ?? throw new InvalidOperationException("SUPERVISOR_TOKEN not found");
    }

    public bool IsHomeAssistantAddon => true;

    public bool IsUserManagementEnabled => false;

    public string GetConfigDirectory() => EnvironmentConfiguration.ConfigDir;

    public async Task<string?> CreateAccessTokenAsync(string clientName)
    {
        try
        {
            var host = Constants.SupervisorCoreUrl;
            using var ws = await WebSocketHelpers.ConnectAndAuthenticateAsync(host, _supervisorToken);

            await WebSocketHelpers.SendMessageAsync(ws, new
            {
                id = 1,
                type = "auth/long_lived_access_token",
                client_name = clientName,
                lifespan = 3650 // 10 years
            });

            var createResponse = await WebSocketHelpers.ReceiveMessageAsync(ws);
            var createResult = JsonDocument.Parse(createResponse);

            if (createResult.RootElement.TryGetProperty("success", out var successProp) 
                && successProp.GetBoolean()
                && createResult.RootElement.TryGetProperty("result", out var resultProp)
                && resultProp.ValueKind == JsonValueKind.String)
            {
                var token = resultProp.GetString();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogInformation("Created long-lived access token for client: {ClientName}", clientName);
                    return token;
                }
            }

            _logger.LogWarning("Failed to create long-lived token: {Response}", createResponse);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating long-lived access token");
            return null;
        }
    }

    public (string host, string token) GetHomeAssistantConnection(Dashboard dashboard)
    {
        // Always use supervisor endpoint with supervisor token for internal communication
        return (Constants.SupervisorCoreUrl, _supervisorToken);
    }

    public UnitResult<string> ValidateConfiguration()
    {
        // In HA add-on mode, all configuration is optional
        // Authentication is handled via ingress, supervisor token is available
        return UnitResult.Success<string>();
    }

    public ClaimsPrincipal? AuthenticateViaIngress(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(Constants.IngressPathHeader))
        {
            return null;
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Constants.HomeAssistantAdminUserId),
            new Claim(ClaimTypes.Name, Constants.HomeAssistantAdminUserName),
            new Claim(Constants.IsSuperUserClaim, "true"),
            new Claim(Constants.HomeAssistantIngressClaim, "true")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public async Task<bool> ProcessIngressPathAsync(HttpContext context, IWebHostEnvironment environment)
    {
        if (!context.Request.Headers.TryGetValue(Constants.IngressPathHeader, out var ingressPathValues))
        {
            return false;
        }

        var ingressPath = ingressPathValues.ToString();
        if (string.IsNullOrWhiteSpace(ingressPath))
        {
            return false;
        }

        if (!ingressPath.StartsWith('/'))
        {
            ingressPath = "/" + ingressPath;
        }

        ingressPath = ingressPath.TrimEnd('/');
        context.Request.PathBase = new PathString(ingressPath);

        // Rewrite index.html with correct base href
        if (context.Request.Path == "/" || context.Request.Path == "/index.html")
        {
            var indexPath = Path.Combine(environment.WebRootPath, "browser", "index.html");
            if (File.Exists(indexPath))
            {
                var html = await File.ReadAllTextAsync(indexPath);
                var baseHref = ingressPath + "/";
                html = html.Replace("<base href=\"/\">", $"<base href=\"{baseHref}\">");
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
                return true;
            }
        }

        return false;
    }

    public void PerformInitialSetup(IServiceProvider serviceProvider)
    {
        // No setup needed in HA add-on mode
        // Ingress handles authentication
    }
}
