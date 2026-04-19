using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Ai;

public interface IAiService
{
    Task<Result<string, string>> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default,
        bool jsonMode = true);
}
