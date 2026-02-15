using EPaperDashboard.Guards;
using Newtonsoft.Json;

namespace EPaperDashboard.Models;

public sealed record HassTokens(string AccessToken, string TokenType, string HassUrl, string ClientId)
{
    [JsonProperty("access_token")]
    public string AccessToken { get; } = Guard.NeitherNullNorWhitespace(AccessToken);

    [JsonProperty("token_type")]
    public string TokenType { get; } = Guard.NeitherNullNorWhitespace(TokenType);

    [JsonProperty("hassUrl")]
    public string HassUrl { get; } = Guard.NeitherNullNorWhitespace(HassUrl);

    [JsonProperty("clientId")]
    public string ClientId { get; } = Guard.NeitherNullNorWhitespace(ClientId);

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; init; } = "";

    [JsonProperty("expires_in")]
    public long ExpiresIn { get; init; } = 315360000;

    [JsonProperty("expires")]
    public long Expires { get; init; } = DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeMilliseconds();
}