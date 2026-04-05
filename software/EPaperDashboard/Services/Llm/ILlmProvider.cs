using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// Abstraction for an LLM provider that can generate text and verify connectivity.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generates a text response for the given prompt.
    /// Returns a failure result if the provider is not configured or the call fails.
    /// </summary>
    Task<Result<string, string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the LLM endpoint is reachable.
    /// Returns a failure result with an error message if connectivity fails.
    /// </summary>
    Task<Result<bool, string>> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The configured timeout in seconds for generate requests.
    /// </summary>
    int TimeoutSeconds { get; }
}
