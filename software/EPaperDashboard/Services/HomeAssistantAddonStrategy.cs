using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using System.Text;
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

    public Task<string?> CreateAccessTokenAsync(string clientName)
    {
        // Long-lived token creation is not supported via supervisor token.
        // The supervisor token is a system-level token only valid for supervisor proxy routes.
        // Dashboards must use OAuth flow to get proper HA user tokens for frontend access.
        _logger.LogWarning("Long-lived token creation via supervisor is not supported. Use OAuth flow.");
        return Task.FromResult<string?>(null);
    }

    public (string host, string token) GetHomeAssistantConnection(Dashboard dashboard)
    {
        // Always route through the supervisor proxy which validates the supervisor token.
        // Using HomeAssistantCoreUrl directly would require a real HA auth token.
        return (Constants.SupervisorCoreUrl, dashboard.AccessToken!);
    }

    public UnitResult<string> ValidateConfiguration()
    {
        // In HA add-on mode, all configuration is optional
        // Authentication is handled via ingress, supervisor token is available
        return UnitResult.Success<string>();
    }

    public Uri? GetOAuthClientUri(HttpContext? context = null)
    {
        // Extract ingress URL from current request context
        if (context?.Request.Headers.TryGetValue(Constants.IngressPathHeader, out var ingressPathValues) == true)
        {
            var ingressPath = ingressPathValues.ToString();
            if (!string.IsNullOrWhiteSpace(ingressPath))
            {
                // Construct full ingress URL: http://homeassistant/api/hassio_ingress/<session>
                var ingressUrl = $"http://homeassistant{ingressPath.TrimEnd('/')}";
                _logger.LogDebug("Using ingress URL from request context: {IngressUrl}", ingressUrl);
                return new Uri(ingressUrl);
            }
        }

        // OAuth not available without ingress context
        _logger.LogWarning("OAuth client URI not available - no ingress header in request context.");
        return null;
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

    public void ApplyPostAuthenticationMiddleware(IApplicationBuilder app, IWebHostEnvironment environment)
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
            // Check for ingress header directly
            if (!context.Request.Headers.TryGetValue(Constants.IngressPathHeader, out var headerValue))
            {
                await next();
                return;
            }

            var path = context.Request.Path.Value ?? "";
            
            // Skip API requests and static file requests (files with extensions)
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                (path.Contains('.') && !path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
            {
                await next();
                return;
            }

            var ingressPath = headerValue.ToString();
            if (string.IsNullOrWhiteSpace(ingressPath))
            {
                await next();
                return;
            }
            
            if (!ingressPath.StartsWith('/'))
            {
                ingressPath = "/" + ingressPath;
            }
            ingressPath = ingressPath.TrimEnd('/');

            var html = _cachedIndexHtml.GetOrAdd(ingressPath, key =>
            {
                var indexPath = Path.Combine(environment.WebRootPath, "browser", "index.html");
                if (!File.Exists(indexPath))
                {
                    _logger.LogError("Index.html not found at {IndexPath}", indexPath);
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
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
            await context.Response.WriteAsync(html);
            return; // Don't call next() after serving the response
        });
    }
}
