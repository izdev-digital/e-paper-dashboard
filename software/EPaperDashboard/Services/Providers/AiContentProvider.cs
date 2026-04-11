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

        ## Formatting
        The content is rendered as markdown. Supported syntax:
        - Headings: # H1, ## H2, ### H3, #### H4
        - Emphasis: **bold**, *italic*, ~~strikethrough~~
        - Lists: unordered (- item), ordered (1. item), nested (indent with spaces)
        - Task lists: - [ ] pending, - [x] completed
        - Blockquotes: > text
        - Horizontal rules: ---
        - Fenced code blocks: ```code```

        ## Icons
        You can embed Font Awesome solid icons inline using the :fa-icon-name: syntax.
        Examples: :fa-sun: :fa-cloud: :fa-house: :fa-calendar: :fa-check: :fa-star:
        Only Font Awesome solid icons are available. Use them to add visual cues.

        ## Constraints
        - Images (![alt](url)) are NOT supported — use :fa-icon: icons for visual elements.
        - HTML tags are NOT supported and will be stripped. Use only markdown syntax.
        - Keep content concise and suitable for a small e-paper display widget.
        - Prefer short paragraphs and lists over long prose.
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

        var effectiveConfig = ResolveAiConfig(dashboard.Value);
        if (effectiveConfig == null || effectiveConfig.ConnectionMode == AiConnectionMode.None)
        {
            logger.LogWarning("AI not configured for dashboard {DashboardId}, skipping ai-content generation", dashboardId);
            return Result.Failure<string, string>("AI is not configured");
        }

        var aiServiceResult = aiServiceFactory.Create(effectiveConfig, dashboardId);
        if (aiServiceResult.IsFailure)
        {
            return Result.Failure<string, string>(aiServiceResult.Error);
        }

        return await aiServiceResult.Value.GenerateCompletionAsync(
            SystemPrompt, prompt, cancellationToken);
    }

    private AiConfig? ResolveAiConfig(Dashboard dashboard)
    {
        if (dashboard.AiConfig != null && dashboard.AiConfig.ConnectionMode == AiConnectionMode.HomeAssistant)
        {
            return dashboard.AiConfig;
        }

        var user = userService.GetUserById(dashboard.UserId);
        return user.HasValue ? user.Value.AiConfig : null;
    }
}
