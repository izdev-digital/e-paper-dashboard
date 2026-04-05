using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// Resolves the appropriate LLM provider for a given user.
/// In Addon/Host mode: returns HomeAssistantLlmProvider.
/// In Standalone mode: returns the user's configured provider.
/// Falls back to NoOpLlmProvider when AI is not configured.
/// </summary>
public interface ILlmProviderFactory
{
    /// <summary>
    /// Gets the LLM provider for the specified user.
    /// </summary>
    /// <param name="userId">The user to resolve a provider for.</param>
    /// <param name="dashboardId">Optional dashboard ID for Home Assistant mode.</param>
    ILlmProvider GetProvider(UserId userId, string? dashboardId = null);
}
