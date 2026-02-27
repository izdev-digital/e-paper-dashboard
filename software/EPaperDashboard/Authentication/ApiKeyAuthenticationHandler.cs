using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EPaperDashboard.Utilities;
using EPaperDashboard.Services;

namespace EPaperDashboard.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DashboardService dashboardService,
    DeviceService deviceService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    private readonly DashboardService _dashboardService = dashboardService;
    private readonly DeviceService _deviceService = deviceService;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HttpHeaderNames.ApiKeyHeaderName, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or empty API Key"));
        }

        // Check dashboard API keys first, then device API keys
        if (_dashboardService.GetDashboardByApiKey(apiKey!).HasNoValue &&
            _deviceService.GetDeviceByApiKey(apiKey!).HasNoValue)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }
        
        var claims = new[] { new Claim("ApiKey", apiKey!) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
