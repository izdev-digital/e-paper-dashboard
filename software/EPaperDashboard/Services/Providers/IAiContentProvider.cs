using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Generates AI content for ai-content widgets at render time.
/// </summary>
public interface IAiContentProvider
{
    /// <summary>
    /// Generates content for the given prompt using the dashboard owner's AI configuration.
    /// </summary>
    Task<Result<string, string>> GenerateContentAsync(
        string dashboardId, string prompt, CancellationToken cancellationToken = default);
}
