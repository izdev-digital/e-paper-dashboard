using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/homeassistant")]
[Authorize]
public class HomeAssistantAuthController(
    ILogger<HomeAssistantAuthController> logger,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private readonly ILogger<HomeAssistantAuthController> _logger = logger;
    private static readonly ConcurrentDictionary<string, AuthState> _pendingAuths = new();

    [HttpPost("start-auth")]
    public IActionResult StartAuth([FromBody] AuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Host))
        {
            return BadRequest(new { error = "Host is required" });
        }

        if (string.IsNullOrWhiteSpace(request.DashboardId))
        {
            return BadRequest(new { error = "DashboardId is required" });
        }

        try
        {
            var hostUri = new Uri(request.Host);
            var state = GenerateRandomString(32);
            var clientId = EnvironmentConfiguration.ClientUri.ToString().TrimEnd('/');
            var redirectUri = $"{clientId}/api/homeassistant/callback";

            var hostUrl = hostUri.ToString().TrimEnd('/');

            _pendingAuths[state] = new AuthState
            {
                Host = hostUrl,
                DashboardId = request.DashboardId,
                CreatedAt = DateTime.UtcNow
            };

            // Build the authorization URL
            var authUrl = $"{hostUrl}/auth/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";

            _logger.LogInformation("Starting auth flow for {Host} with state {State}", hostUri, state);

            return Ok(new
            {
                authUrl,
                state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Home Assistant authentication");
            return StatusCode(500, new { error = $"Failed to start authentication: {ex.Message}" });
        }
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            // Home Assistant returned an error
            _logger.LogWarning("Home Assistant authorization failed: {Error}", error);
            return GetAutoSubmitForm(state, null, error);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest("Missing code or state parameter");
        }

        if (!_pendingAuths.TryRemove(state, out var authState))
        {
            return BadRequest("Invalid or expired state parameter");
        }

        // Check if state is too old (5 minutes timeout)
        if (DateTime.UtcNow - authState.CreatedAt > TimeSpan.FromMinutes(5))
        {
            return BadRequest("Authentication request expired");
        }

        try
        {
            var clientId = EnvironmentConfiguration.ClientUri.ToString().TrimEnd('/');
            var tokenUrl = $"{authState.Host}/auth/token";

            // Exchange authorization code for tokens
            var httpClient = httpClientFactory.CreateClient();
            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "client_id", clientId }
            });

            _logger.LogInformation("Exchanging code for token at {TokenUrl}", tokenUrl);
            var response = await httpClient.PostAsync(tokenUrl, requestBody);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token exchange failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return GetAutoSubmitForm(authState.DashboardId, null, $"Token exchange failed: {response.StatusCode}");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            if (tokenResponse?.AccessToken == null)
            {
                return GetAutoSubmitForm(authState.DashboardId, null, "Invalid token response");
            }

            _logger.LogInformation("Successfully obtained access token");

            // Return auto-submit form that POSTs back to the edit page
            return GetAutoSubmitForm(authState.DashboardId, tokenResponse.AccessToken, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during token exchange");
            var errorMsg = "Unable to connect to Home Assistant. Please ensure the Home Assistant instance is accessible from this server";
            if (ex.InnerException is System.Net.Sockets.SocketException)
            {
                errorMsg += " and check that the Host URL is correct.";
            }
            return GetAutoSubmitForm(authState.DashboardId, null, errorMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token exchange");
            return GetAutoSubmitForm(authState.DashboardId, null, $"Unexpected error: {ex.Message}");
        }
    }

    private ContentResult GetAutoSubmitForm(string dashboardId, string? accessToken, string? error)
    {
        var actionUrl = $"/Dashboards/Edit/{Uri.EscapeDataString(dashboardId)}";
        var accessTokenValue = accessToken ?? string.Empty;
        var errorValue = error ?? string.Empty;

        var html = $$"""
<!DOCTYPE html>
<html>
<head>
    <title>Redirecting...</title>
</head>
<body>
    <form id="authForm" method="post" action="{{actionUrl}}">
        <input type="hidden" name="auth_callback" value="true" />
        <input type="hidden" name="access_token" value="{{accessTokenValue}}" />
        <input type="hidden" name="auth_error" value="{{errorValue}}" />
    </form>
    <script>
        document.getElementById('authForm').submit();
    </script>
</body>
</html>
""";
        return Content(html, "text/html");
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        var randomBytes = new byte[length];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(result);
    }

    public class AuthRequest
    {
        public string Host { get; set; } = string.Empty;
        public string DashboardId { get; set; } = string.Empty;
    }

    private class AuthState
    {
        public string Host { get; set; } = string.Empty;
        public string DashboardId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    private class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
