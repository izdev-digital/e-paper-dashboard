using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;

namespace EPaperDashboard.Services;

/// <summary>
/// Deployment strategy for Home Assistant add-on mode.
/// Uses supervisor token for authentication and internal networking.
/// </summary>
public class HomeAssistantAddonStrategy : IDeploymentStrategy
{
    private readonly ILogger<HomeAssistantAddonStrategy> _logger;
    private readonly string _supervisorToken;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _cachedIndexHtml = new();

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
                lifespan = 3650
            });

            var createResponse = await WebSocketHelpers.ReceiveMessageAsync(ws);
            var createResult = JsonDocument.Parse(createResponse);

            if (!createResult.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                _logger.LogWarning("Failed to create long-lived token: {Response}", createResponse);
                return null;
            }

            if (!createResult.RootElement.TryGetProperty("result", out var resultProp) || resultProp.ValueKind != JsonValueKind.String)
            {
                _logger.LogWarning("Failed to create long-lived token: {Response}", createResponse);
                return null;
            }

            var token = resultProp.GetString();
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Failed to create long-lived token: {Response}", createResponse);
                return null;
            }

            _logger.LogInformation("Created long-lived access token for client: {ClientName}", clientName);
            return token;
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

    public Task<bool> ProcessIngressPathAsync(HttpContext context, IWebHostEnvironment environment)
    {
        if (!context.Request.Headers.TryGetValue(Constants.IngressPathHeader, out var ingressPathValues))
        {
            return Task.FromResult(false);
        }

        var ingressPath = ingressPathValues.ToString();
        if (string.IsNullOrWhiteSpace(ingressPath))
        {
            return Task.FromResult(false);
        }

        if (!ingressPath.StartsWith('/'))
        {
            ingressPath = "/" + ingressPath;
        }

        ingressPath = ingressPath.TrimEnd('/');
        
        var originalPath = context.Request.Path.Value ?? "/";
        context.Request.PathBase = new PathString(ingressPath);
        
        if (originalPath.StartsWith(ingressPath, StringComparison.OrdinalIgnoreCase))
        {
            var newPath = originalPath.Substring(ingressPath.Length);
            if (string.IsNullOrEmpty(newPath))
            {
                newPath = "/";
            }
            context.Request.Path = new PathString(newPath);
        }
        
        context.Items["IngressPath"] = ingressPath;
        
        return Task.FromResult(false);
    }

    public void PerformInitialSetup(IServiceProvider serviceProvider)
    {
    }

    public void ApplyMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.Use(async (context, next) =>
        {
            if (environment.IsDevelopment())
            {
                await next();
                return;
            }

            await ProcessIngressPathAsync(context, environment);
            await next();
        });
    }

    public void ApplyPostRoutingMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.Use(async (context, next) =>
        {
            var principal = AuthenticateViaIngress(context);
            if (principal == null)
            {
                await next();
                return;
            }

            context.User = principal;

            if (IsUserManagementEnabled)
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var isUserManagementEndpoint = path.StartsWith("/api/auth/") || path.StartsWith("/api/users/");
            
            if (isUserManagementEndpoint && path != "/api/auth/current")
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { message = "User management is disabled in Home Assistant add-on mode" });
                return;
            }
            
            await next();
        });
    }

    public void ApplyPostStaticFilesMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Items.ContainsKey("IngressPath"))
            {
                await next();
                return;
            }

            var isIndexRequest = context.Request.Path == "/" || 
                                 context.Request.Path == "/index.html" || 
                                 !context.Request.Path.HasValue;
            
            if (!isIndexRequest)
            {
                await next();
                return;
            }

            var ingressPath = context.Items["IngressPath"]?.ToString();
            if (string.IsNullOrEmpty(ingressPath))
            {
                await next();
                return;
            }

            var html = _cachedIndexHtml.GetOrAdd(ingressPath, key =>
            {
                var indexPath = Path.Combine(environment.WebRootPath, "browser", "index.html");
                if (!File.Exists(indexPath))
                {
                    return string.Empty;
                }

                var content = File.ReadAllText(indexPath);
                var baseHref = key + "/";
                return content.Replace("<base href=\"/\">", $"<base href=\"{baseHref}\">");
            });

            if (string.IsNullOrEmpty(html))
            {
                await next();
                return;
            }
            
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        });
    }
}
