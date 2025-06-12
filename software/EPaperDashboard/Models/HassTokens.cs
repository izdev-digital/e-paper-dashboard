using Newtonsoft.Json;

namespace EPaperDashboard.Models;

public sealed class HassTokens
{
    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("token_type")]
    public string? TokenType { get; set; }

    [JsonProperty("hassUrl")]
    public string? HassUrl { get; set; }

    [JsonProperty("clientId")]
    public string? ClientId { get; set; }
}