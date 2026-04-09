using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiConnectionMode
{
    None = 0,
    HomeAssistant = 1,
    Direct = 2
}

public class AiConfig
{
    public AiConnectionMode ConnectionMode { get; set; } = AiConnectionMode.None;
    public string? DirectEndpoint { get; set; }
    public string? DirectApiKey { get; set; }
    public string? DirectModel { get; set; }
    public string? HomeAssistantAgentId { get; set; }
}
