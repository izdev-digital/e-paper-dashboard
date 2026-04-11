using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Ai;

public sealed class AiPreGenerationService(
    ILogger<AiPreGenerationService> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AI Pre-Generation Service started");

        // Wait for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndPreGenerate(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during AI pre-generation check");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }

        logger.LogInformation("AI Pre-Generation Service stopped");
    }

    private async Task CheckAndPreGenerate(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<DashboardService>();
        var aiGenerationService = scope.ServiceProvider.GetRequiredService<AiDashboardGenerationService>();

        var allDashboards = dashboardService.GetAllDashboards();
        var now = DateTimeOffset.Now;

        foreach (var dashboard in allDashboards)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!ShouldPreGenerate(dashboard, now))
            {
                continue;
            }

            try
            {
                logger.LogInformation(
                    "Pre-generating AI content for dashboard {DashboardId} ({DashboardName})",
                    dashboard.Id, dashboard.Name);

                var result = await aiGenerationService.GenerateAsync(dashboard, cancellationToken: cancellationToken);

                if (result.IsSuccess)
                {
                    logger.LogInformation(
                        "Successfully pre-generated {WidgetCount} AI widgets for dashboard {DashboardId}",
                        result.Value.Widgets.Count, dashboard.Id);
                }
                else
                {
                    logger.LogWarning(
                        "AI pre-generation failed for dashboard {DashboardId}: {Error}",
                        dashboard.Id, result.Error);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Exception during AI pre-generation for dashboard {DashboardId}",
                    dashboard.Id);
            }
        }
    }

    private static bool ShouldPreGenerate(Dashboard dashboard, DateTimeOffset now)
    {
        // Only process AI-enabled custom dashboards
        if (!dashboard.IsAiEnabled
            || dashboard.RenderingMode != RenderingMode.Custom
            || string.IsNullOrWhiteSpace(dashboard.AiPrompt))
        {
            return false;
        }

        // Must have scheduled update times
        if (dashboard.UpdateTimes == null || dashboard.UpdateTimes.Count == 0)
        {
            return false;
        }

        var leadTimeMinutes = dashboard.AiLeadTimeMinutes > 0 ? dashboard.AiLeadTimeMinutes : 5;
        var nowTimeOnly = TimeOnly.FromDateTime(now.LocalDateTime);

        // Check if any scheduled time is within the lead time window
        foreach (var updateTime in dashboard.UpdateTimes)
        {
            var preGenTime = updateTime.AddMinutes(-leadTimeMinutes);
            var minutesUntilUpdate = GetMinutesDifference(nowTimeOnly, updateTime);
            var minutesSincePre = GetMinutesDifference(preGenTime, nowTimeOnly);

            // We're in the pre-generation window: between [updateTime - leadTime] and [updateTime]
            if (minutesUntilUpdate >= 0 && minutesUntilUpdate <= leadTimeMinutes)
            {
                // Check if we already generated for this window
                if (dashboard.LastAiGenerationTime.HasValue)
                {
                    var lastGenTime = TimeOnly.FromDateTime(dashboard.LastAiGenerationTime.Value.LocalDateTime);
                    var minutesSinceLastGen = GetMinutesDifference(preGenTime, lastGenTime);

                    // Already generated within this window
                    if (minutesSinceLastGen >= 0 && minutesSinceLastGen <= leadTimeMinutes)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private static double GetMinutesDifference(TimeOnly from, TimeOnly to)
    {
        var diff = to.ToTimeSpan() - from.ToTimeSpan();
        if (diff.TotalMinutes < -720) // Handle midnight crossing
            diff = diff.Add(TimeSpan.FromHours(24));
        return diff.TotalMinutes;
    }
}
