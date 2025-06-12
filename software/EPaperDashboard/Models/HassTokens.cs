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
}