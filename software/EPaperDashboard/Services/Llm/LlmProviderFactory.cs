using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using Microsoft.AspNetCore.DataProtection;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// Resolves the appropriate LLM provider for a given user.
/// In Addon/Host mode: returns HomeAssistantLlmProvider when a dashboardId is provided.
/// In Standalone mode: returns the user's configured provider from LiteDB.
/// Falls back to NoOpLlmProvider when AI is not configured.
/// </summary>
public sealed class LlmProviderFactory(
    IDeploymentStrategy deploymentStrategy,
    IUserLlmConfigRepository configRepository,
    HomeAssistantConnectionService haConnectionService,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider) : ILlmProviderFactory
{
    private readonly IDataProtector _dataProtector =
        dataProtectionProvider.CreateProtector("EPaperDashboard.LlmApiKey");

    public ILlmProvider GetProvider(UserId userId, string? dashboardId = null)
    {
        // In Addon/Host mode, use HA conversation API (requires a dashboard context)
        if (deploymentStrategy.Mode != DeploymentMode.Standalone)
        {
            if (!string.IsNullOrWhiteSpace(dashboardId))
            {
                return new HomeAssistantLlmProvider(haConnectionService, dashboardId);
            }
            return new NoOpLlmProvider();
        }

        // Standalone mode: resolve per-user config from LiteDB
        var configMaybe = configRepository.FindByUserId(userId);
        if (configMaybe.HasNoValue || !configMaybe.Value.Enabled)
        {
            return new NoOpLlmProvider();
        }

        var config = configMaybe.Value;

        // Decrypt API key if stored
        string? decryptedApiKey = null;
        if (!string.IsNullOrWhiteSpace(config.EncryptedApiKey))
        {
            try
            {
                decryptedApiKey = _dataProtector.Unprotect(config.EncryptedApiKey);
            }
            catch
            {
                // Decryption failed — treat as no key set
            }
        }

        // Build a config copy with the decrypted key for provider use
        var resolvedConfig = new UserLlmConfig
        {
            Id = config.Id,
            UserId = config.UserId,
            Enabled = config.Enabled,
            ProviderType = config.ProviderType,
            BaseUrl = config.BaseUrl,
            Model = config.Model,
            EncryptedApiKey = decryptedApiKey,
            Temperature = config.Temperature,
            TimeoutSeconds = config.TimeoutSeconds
        };

        return config.ProviderType switch
        {
            "ollama" => new OllamaLlmProvider(httpClientFactory, resolvedConfig),
            "openai" => new OpenAiCompatibleLlmProvider(httpClientFactory, resolvedConfig),
            _ => new NoOpLlmProvider()
        };
    }
}
