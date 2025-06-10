using System.ComponentModel.DataAnnotations;
using System.Web;
using EPaperDashboard.Services;
using EPaperDashboard.Utilities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace EPaperDashboard.Controllers;

[ApiController]
public sealed class HassController(IHttpClientFactory httpClientFactory, ILogger<HassController> logger, IHassRepository hassRepository) : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<HassController> _logger = logger;
    private readonly IHassRepository _hassRepository = hassRepository;

    [HttpGet("auth")]
    public IActionResult GetLoginUrl()
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query.Add("client_id", EnvironmentConfiguration.ClientUri.AbsoluteUri);
        query.Add("redirect_uri", new Uri(EnvironmentConfiguration.ClientUri, "auth_callback").AbsoluteUri);
        var authUri = new Uri(EnvironmentConfiguration.HassUri, $"auth/authorize?{query}");
        return Redirect(authUri.AbsoluteUri);
    }

    [HttpGet("auth_callback")]
    public async Task<IActionResult> AuthCallbackAsync([Required][FromQuery] string code, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received auth code: {0}", code);
        var accessToken = await ExchangeCodeForToken(code, cancellationToken);
        _hassRepository.StoreToken(accessToken);
        return NoContent();
    }

    internal async Task<AccessTokenDto?> ExchangeCodeForToken(string code, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HassHttpClientName);
        var values = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "client_id", EnvironmentConfiguration.ClientUri.AbsoluteUri }
        };

        var content = new FormUrlEncodedContent(values);
        var response = await httpClient.PostAsync("auth/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogInformation("Token received: {0}", json);
        return JsonConvert.DeserializeObject<AccessTokenDto>(json);
    }
}

public sealed class AccessTokenDto
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public long? ExpiresIn { get; set; }

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("token_type")]
    public string? TokenType { get; set; }
}