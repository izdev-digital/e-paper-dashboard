using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

public interface IAiContentProvider
{
    Task<Result<string, string>> GenerateContentAsync(
        string dashboardId, string prompt, CancellationToken cancellationToken = default);
}
