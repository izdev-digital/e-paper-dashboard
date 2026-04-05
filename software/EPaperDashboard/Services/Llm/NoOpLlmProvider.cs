using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Llm;

/// <summary>
/// Default LLM provider used when no AI provider is configured.
/// All operations return a graceful "not configured" failure.
/// </summary>
public sealed class NoOpLlmProvider : ILlmProvider
{
    public int TimeoutSeconds => 0;

    public Task<Result<string, string>> GenerateAsync(string prompt, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<string, string>("AI provider is not configured."));

    public Task<Result<bool, string>> TestConnectionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<bool, string>("AI provider is not configured."));
}
