namespace EPaperDashboard.Models;

/// <summary>
/// Per-user LLM provider configuration stored in LiteDB.
/// </summary>
public class UserLlmConfig
{
    public UserLlmConfigId Id { get; set; } = UserLlmConfigId.Empty;
    public UserId UserId { get; set; } = UserId.Empty;
    public bool Enabled { get; set; } = false;

    /// <summary>"ollama" | "openai" | "none"</summary>
    public string ProviderType { get; set; } = "none";

    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>API key encrypted at rest via IDataProtector. Null if not set.</summary>
    public string? EncryptedApiKey { get; set; }

    public double Temperature { get; set; } = 0.1;
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Transient decrypted API key for use by providers at runtime.
    /// Never persisted to the database.
    /// </summary>
    [LiteDB.BsonIgnore]
    public string? PlainApiKey { get; set; }
}
