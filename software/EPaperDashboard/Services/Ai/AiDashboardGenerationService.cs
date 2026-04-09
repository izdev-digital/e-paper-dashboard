using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Services.Providers;

namespace EPaperDashboard.Services.Ai;

/// <summary>
/// Orchestrates AI dashboard generation: fetches data from providers,
/// builds prompts, calls the LLM, validates the response, and stores
/// the generated widgets on the dashboard.
/// </summary>
public sealed class AiDashboardGenerationService(
    IAiServiceFactory aiServiceFactory,
    IEntityStateProvider entityStateProvider,
    ITodoDataProvider todoDataProvider,
    ICalendarDataProvider calendarDataProvider,
    IWeatherForecastProvider weatherForecastProvider,
    IRssFeedDataProvider rssFeedDataProvider,
    DashboardService dashboardService,
    UserService userService,
    AiPromptBuilder promptBuilder,
    ILogger<AiDashboardGenerationService> logger)
{
    public async Task<Result<List<WidgetConfig>, string>> GenerateAsync(
        Dashboard dashboard,
        CancellationToken cancellationToken = default)
    {
        // Resolve user's AI config
        var userMaybe = userService.GetUserById(dashboard.UserId);
        if (userMaybe.HasNoValue)
        {
            return "Dashboard owner not found";
        }

        var user = userMaybe.Value;
        if (user.AiConfig == null || user.AiConfig.ConnectionMode == AiConnectionMode.None)
        {
            return "AI is not configured. Set up an AI connection in user settings.";
        }

        // Create AI service from user config
        var aiServiceResult = aiServiceFactory.Create(user.AiConfig, dashboard.Id.ToString());
        if (aiServiceResult.IsFailure)
        {
            return aiServiceResult.Error;
        }

        var aiService = aiServiceResult.Value;

        // Fetch data from providers for entities the AI can use
        var aiData = await FetchDataForAi(dashboard);

        // Build the layout config to pass to prompt builder (for grid/color info + pinned widgets)
        var layoutConfig = dashboard.LayoutConfig ?? CreateDefaultLayoutConfig(dashboard);

        // Build prompts
        var (systemPrompt, userPrompt) = promptBuilder.BuildPrompt(
            dashboard,
            layoutConfig,
            aiData.EntityStates,
            aiData.TodoItems,
            aiData.CalendarEvents,
            aiData.WeatherForecasts,
            aiData.RssFeedEntries);

        logger.LogInformation(
            "Generating AI dashboard for {DashboardId} ({DashboardName}), prompt length: {SystemLen}+{UserLen} chars",
            dashboard.Id, dashboard.Name, systemPrompt.Length, userPrompt.Length);

        // Call LLM
        var completionResult = await aiService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);
        if (completionResult.IsFailure)
        {
            return completionResult.Error;
        }

        // Parse and validate the response
        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;
        var parseResult = ParseAiResponse(completionResult.Value, gridCols, gridRows);
        if (parseResult.IsFailure)
        {
            logger.LogWarning("AI response parsing failed: {Error}. Raw response: {Response}",
                parseResult.Error, completionResult.Value);
            return parseResult.Error;
        }

        var generatedWidgets = parseResult.Value;

        // Store the generated widgets
        dashboard.AiGeneratedWidgets = generatedWidgets;
        dashboard.LastAiGenerationTime = DateTimeOffset.UtcNow;
        dashboardService.UpdateDashboard(dashboard);

        logger.LogInformation(
            "AI generated {WidgetCount} widgets for dashboard {DashboardId}",
            generatedWidgets.Count, dashboard.Id);

        return generatedWidgets;
    }

    private async Task<AiDataSnapshot> FetchDataForAi(Dashboard dashboard)
    {
        var data = new AiDataSnapshot();
        var entityIds = dashboard.AiDataSourceEntityIds ?? new List<string>();
        if (entityIds.Count == 0)
        {
            logger.LogWarning("Dashboard {DashboardId} has no AI data source entities configured", dashboard.Id);
            return data;
        }

        var dashboardId = dashboard.Id.ToString();

        try
        {
            // Fetch all entity states with a timeout
            using var statesCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var statesResult = await entityStateProvider.FetchEntityStatesAsync(dashboardId, entityIds.ToArray());
                if (statesResult.IsSuccess)
                {
                    foreach (var state in statesResult.Value)
                        data.EntityStates[state.EntityId] = state;
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                logger.LogWarning("Entity state fetch timed out for dashboard {DashboardId}", dashboard.Id);
            }

            // Fetch per-domain data with individual timeouts
            foreach (var entityId in entityIds)
            {
                var domain = entityId.Split('.').FirstOrDefault() ?? "";

                try
                {
                    switch (domain)
                    {
                        case "calendar":
                            var calResult = await calendarDataProvider.FetchCalendarEventsAsync(dashboardId, entityId, 168);
                            if (calResult.IsSuccess)
                                data.CalendarEvents[entityId] = calResult.Value;
                            break;

                        case "todo":
                            var todoResult = await todoDataProvider.FetchTodoItemsAsync(dashboardId, entityId);
                            if (todoResult.IsSuccess)
                                data.TodoItems[entityId] = todoResult.Value;
                            break;

                        case "weather":
                            var forecastResult = await weatherForecastProvider.FetchWeatherForecastAsync(dashboardId, entityId, "daily");
                            if (forecastResult.IsSuccess
                                && forecastResult.Value.TryGetValue("forecast", out var forecastVal)
                                && forecastVal is List<object?> forecastList)
                            {
                                data.WeatherForecasts[entityId] = forecastList;
                            }
                            break;

                        default:
                            if (entityId.Contains("feed", StringComparison.OrdinalIgnoreCase))
                            {
                                var rssResult = await rssFeedDataProvider.FetchRssFeedEntriesAsync(dashboardId, entityId);
                                if (rssResult.IsSuccess)
                                    data.RssFeedEntries[entityId] = rssResult.Value;
                            }
                            break;
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
                {
                    logger.LogWarning("Data fetch timed out for entity {EntityId} on dashboard {DashboardId}", entityId, dashboard.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch data for AI generation on dashboard {DashboardId}", dashboard.Id);
        }

        return data;
    }

    private Result<List<WidgetConfig>, string> ParseAiResponse(
        string response, int gridCols, int gridRows)
    {
        try
        {
            // Strip markdown code fences if present
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    json = json[(firstNewline + 1)..];
                }
                if (json.EndsWith("```"))
                {
                    json = json[..^3];
                }
                json = json.Trim();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("widgets", out var widgetsArray)
                || widgetsArray.ValueKind != JsonValueKind.Array)
            {
                return "AI response does not contain a 'widgets' array";
            }

            var widgets = new List<WidgetConfig>();

            foreach (var w in widgetsArray.EnumerateArray())
            {
                var id = w.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var type = w.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
                {
                    continue;
                }

                // Validate widget type
                if (!IsKnownWidgetType(type))
                {
                    logger.LogWarning("AI generated unknown widget type '{Type}', skipping", type);
                    continue;
                }

                // Parse position
                if (!w.TryGetProperty("position", out var posEl))
                {
                    continue;
                }

                var x = posEl.TryGetProperty("x", out var xEl) ? xEl.GetInt32() : 0;
                var y = posEl.TryGetProperty("y", out var yEl) ? yEl.GetInt32() : 0;
                var width = posEl.TryGetProperty("w", out var wEl) ? wEl.GetInt32() : 1;
                var height = posEl.TryGetProperty("h", out var hEl) ? hEl.GetInt32() : 1;

                // Clamp to grid bounds
                x = Math.Max(0, Math.Min(x, gridCols - 1));
                y = Math.Max(0, Math.Min(y, gridRows - 1));
                width = Math.Max(1, Math.Min(width, gridCols - x));
                height = Math.Max(1, Math.Min(height, gridRows - y));

                var config = w.TryGetProperty("config", out var configEl)
                    ? configEl.Clone()
                    : JsonSerializer.SerializeToElement(new { });

                string? titleOverride = w.TryGetProperty("titleOverride", out var toEl)
                    ? toEl.GetString()
                    : null;

                widgets.Add(new WidgetConfig
                {
                    Id = id,
                    Type = type,
                    Position = new WidgetPosition
                    {
                        X = x,
                        Y = y,
                        W = width,
                        H = height
                    },
                    Config = config,
                    TitleOverride = titleOverride
                });
            }

            if (widgets.Count == 0)
            {
                return "AI generated no valid widgets";
            }

            return widgets;
        }
        catch (JsonException ex)
        {
            return $"AI response is not valid JSON: {ex.Message}";
        }
    }

    private static bool IsKnownWidgetType(string type) =>
        type is "header" or "markdown" or "calendar" or "weather" or "weather-forecast"
            or "todo" or "rss-feed" or "graph" or "app-icon" or "image" or "version";

    private static Models.LayoutConfig CreateDefaultLayoutConfig(Dashboard dashboard)
    {
        var (width, height) = dashboard.GetEffectiveSize();
        return new Models.LayoutConfig
        {
            Width = width,
            Height = height,
            GridCols = 12,
            GridRows = 8,
            ColorScheme = new ColorScheme
            {
                Name = "Default",
                Palette = new List<string> { "#000000", "#ffffff", "#ff0000" },
                Background = "#ffffff",
                CanvasBackgroundColor = "#ffffff",
                WidgetBackgroundColor = "#ffffff",
                WidgetBorderColor = "#000000",
                WidgetTitleTextColor = "#000000",
                WidgetTextColor = "#000000",
                IconColor = "#ff0000",
                Foreground = "#000000",
                Accent = "#ff0000",
                Text = "#000000"
            },
            Widgets = new List<WidgetConfig>(),
            CanvasPadding = 8,
            WidgetGap = 8,
            WidgetBorder = 1,
            WidgetPadding = 8,
            TitleFontSize = 14,
            TextFontSize = 12,
            TitleFontWeight = 700,
            TextFontWeight = 400
        };
    }
}
