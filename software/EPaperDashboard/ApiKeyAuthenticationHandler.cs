using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EPaperDashboard.Data;
using EPaperDashboard.Utilities;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DashboardService dashboardService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    private readonly DashboardService _dashboardService = dashboardService;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HttpHeaderNames.ApiKeyHeaderName, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or empty API Key"));
        }
        var dashboard = _dashboardService.GetDashboardByApiKey(apiKey!);
        if (dashboard == null)
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
