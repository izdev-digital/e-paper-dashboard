using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Services.Ai;

namespace EPaperDashboard.Services.Providers;

public sealed class AiContentProvider(
    DashboardService dashboardService,
    UserService userService,
    IAiServiceFactory aiServiceFactory,
    ILogger<AiContentProvider> logger) : IAiContentProvider
{
    private const string SystemPrompt = """
        You are an e-paper dashboard content writer. Generate content based on the user's prompt.
        Return ONLY the content text — no JSON wrapping, no code fences.
        Use basic markdown formatting: headings (#-####), **bold**, *italic*, lists, blockquotes.
        Keep content concise and suitable for a small e-paper display widget.
        """;

    public async Task<Result<string, string>> GenerateContentAsync(
        string dashboardId, string prompt, CancellationToken cancellationToken)
    {
        if (!DashboardId.TryParse(dashboardId, out var id))
        {
            return Result.Failure<string, string>("Invalid dashboard ID");
        }

        var dashboard = dashboardService.GetDashboardById(id);
        if (dashboard.HasNoValue)
        {
            return Result.Failure<string, string>("Dashboard not found");
        }

        var user = userService.GetUserById(dashboard.Value.UserId);
        if (user.HasNoValue || user.Value.AiConfig == null
            || user.Value.AiConfig.ConnectionMode == AiConnectionMode.None)
        {
            logger.LogWarning("AI not configured for dashboard {DashboardId}, skipping ai-content generation", dashboardId);
            return Result.Failure<string, string>("AI is not configured");
        }

        var aiServiceResult = aiServiceFactory.Create(user.Value.AiConfig, dashboardId);
        if (aiServiceResult.IsFailure)
        {
            return Result.Failure<string, string>(aiServiceResult.Error);
        }

        return await aiServiceResult.Value.GenerateCompletionAsync(
            SystemPrompt, prompt, cancellationToken);
    }
}
