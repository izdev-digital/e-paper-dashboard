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
    public async Task<Result<AiGenerationResult, string>> GenerateAsync(
        Dashboard dashboard,
        string? promptOverride = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve user's AI config
        var userMaybe = userService.GetUserById(dashboard.UserId);
        if (userMaybe.HasNoValue)
        {
            return StoreError(dashboard, "Dashboard owner not found");
        }

        var user = userMaybe.Value;
        if (user.AiConfig == null || user.AiConfig.ConnectionMode == AiConnectionMode.None)
        {
            return StoreError(dashboard, "AI is not configured. Set up an AI connection in user settings.");
        }

        // Create AI service from user config
        var aiServiceResult = aiServiceFactory.Create(user.AiConfig, dashboard.Id.ToString());
        if (aiServiceResult.IsFailure)
        {
            return StoreError(dashboard, aiServiceResult.Error);
        }

        // Apply prompt override if provided (avoids save-then-generate race)
        if (!string.IsNullOrWhiteSpace(promptOverride))
        {
            dashboard.AiPrompt = promptOverride;
        }

        var aiService = aiServiceResult.Value;

        // Fetch data from providers for entities the AI can use
        var aiData = await FetchDataForAi(dashboard);

        // Build data summary for the response
        var dataSummary = new AiDataSummary
        {
            EntityStates = aiData.EntityStates.Count,
            TodoLists = [.. aiData.TodoItems.Keys],
            Calendars = [.. aiData.CalendarEvents.Keys],
            WeatherEntities = [.. aiData.WeatherForecasts.Keys],
            RssFeeds = [.. aiData.RssFeedEntries.Keys]
        };

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

        var totalPromptChars = systemPrompt.Length + userPrompt.Length;
        var promptTokenEstimate = totalPromptChars / 4;

        logger.LogInformation(
            "Generating AI dashboard for {DashboardId} ({DashboardName}), prompt length: {SystemLen}+{UserLen} chars (~{Tokens} tokens)",
            dashboard.Id, dashboard.Name, systemPrompt.Length, userPrompt.Length, promptTokenEstimate);

        // Call LLM
        var completionResult = await aiService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);
        if (completionResult.IsFailure)
        {
            return StoreError(dashboard, completionResult.Error);
        }

        // Parse and validate the response
        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;
        var parseResult = ParseAiResponse(completionResult.Value, gridCols, gridRows);
        if (parseResult.IsFailure)
        {
            logger.LogWarning("AI response parsing failed: {Error}. Raw response: {Response}",
                parseResult.Error, completionResult.Value);
            return StoreError(dashboard, parseResult.Error);
        }

        var generatedWidgets = parseResult.Value;
        var pinnedWidgets = layoutConfig.Widgets;

        // Verification pass: check for overlaps and ask AI to fix if needed
        if (AiPromptBuilder.HasOverlaps(pinnedWidgets, generatedWidgets, gridCols, gridRows))
        {
            logger.LogInformation(
                "Overlap detected in AI output for dashboard {DashboardId}, running verification pass",
                dashboard.Id);

            var verificationPrompt = promptBuilder.BuildVerificationPrompt(
                pinnedWidgets, generatedWidgets, gridCols, gridRows);

            var verifyResult = await aiService.GenerateCompletionAsync(
                verificationPrompt, "Fix any overlapping widgets and return the corrected layout.", cancellationToken);

            if (verifyResult.IsSuccess)
            {
                var verifyParseResult = ParseAiResponse(verifyResult.Value, gridCols, gridRows);
                if (verifyParseResult.IsSuccess)
                {
                    if (!AiPromptBuilder.HasOverlaps(pinnedWidgets, verifyParseResult.Value, gridCols, gridRows))
                    {
                        generatedWidgets = verifyParseResult.Value;
                        logger.LogInformation("Verification pass resolved overlaps for dashboard {DashboardId}", dashboard.Id);
                    }
                    else
                    {
                        logger.LogWarning("Verification pass still has overlaps for dashboard {DashboardId}, removing conflicting widgets", dashboard.Id);
                        generatedWidgets = RemoveOverlappingWidgets(pinnedWidgets, verifyParseResult.Value, gridCols, gridRows);
                    }
                }
                else
                {
                    logger.LogWarning("Verification pass response parsing failed, removing conflicting widgets from original output");
                    generatedWidgets = RemoveOverlappingWidgets(pinnedWidgets, generatedWidgets, gridCols, gridRows);
                }
            }
            else
            {
                logger.LogWarning("Verification pass LLM call failed: {Error}, removing conflicting widgets", verifyResult.Error);
                generatedWidgets = RemoveOverlappingWidgets(pinnedWidgets, generatedWidgets, gridCols, gridRows);
            }
        }

        // Store the generated widgets and clear any previous error
        generatedWidgets = ValidateAndRepairWidgets(generatedWidgets, aiData, dashboard);
        if (generatedWidgets.Count == 0)
        {
            return StoreError(dashboard, "All AI-generated widgets were invalid after validation");
        }

        dashboard.AiGeneratedWidgets = generatedWidgets;
        dashboard.LastAiGenerationTime = DateTimeOffset.UtcNow;
        dashboard.LastAiGenerationError = null;
        dashboardService.UpdateDashboard(dashboard);

        logger.LogInformation(
            "AI generated {WidgetCount} widgets for dashboard {DashboardId}",
            generatedWidgets.Count, dashboard.Id);

        return new AiGenerationResult
        {
            Widgets = generatedWidgets,
            DataSummary = dataSummary,
            PromptTokenEstimate = promptTokenEstimate
        };
    }

    private Result<AiGenerationResult, string> StoreError(Dashboard dashboard, string error)
    {
        dashboard.LastAiGenerationError = error;
        dashboardService.UpdateDashboard(dashboard);
        return Result.Failure<AiGenerationResult, string>(error);
    }

    private async Task<AiDataSnapshot> FetchDataForAi(Dashboard dashboard)
    {
        var data = new AiDataSnapshot();
        var dashboardId = dashboard.Id.ToString();

        // Fetch all provider data in parallel
        var entityStatesTask = SafeFetchAsync(() => entityStateProvider.FetchAllEntityStatesAsync(dashboardId));
        var todoTask = SafeFetchAsync(() => todoDataProvider.FetchAllTodoItemsAsync(dashboardId));
        var calendarTask = SafeFetchAsync(() => calendarDataProvider.FetchAllCalendarEventsAsync(dashboardId));
        var weatherTask = SafeFetchAsync(() => weatherForecastProvider.FetchAllWeatherForecastsAsync(dashboardId));
        var rssTask = SafeFetchAsync(() => rssFeedDataProvider.FetchAllRssFeedEntriesAsync(dashboardId));

        await Task.WhenAll(entityStatesTask, todoTask, calendarTask, weatherTask, rssTask);

        var entityStates = await entityStatesTask;
        if (entityStates != null)
        {
            foreach (var state in entityStates)
                data.EntityStates[state.EntityId] = state;
        }

        var todoItems = await todoTask;
        if (todoItems != null)
            data.TodoItems = todoItems;

        var calendarEvents = await calendarTask;
        if (calendarEvents != null)
            data.CalendarEvents = calendarEvents;

        var weatherForecasts = await weatherTask;
        if (weatherForecasts != null)
            data.WeatherForecasts = weatherForecasts;

        var rssEntries = await rssTask;
        if (rssEntries != null)
            data.RssFeedEntries = rssEntries;

        logger.LogInformation(
            "AI data snapshot for dashboard {DashboardId}: {States} entity states, {Todo} todo lists, {Cal} calendars, {Weather} weather entities, {Rss} RSS feeds",
            dashboard.Id, data.EntityStates.Count, data.TodoItems.Count,
            data.CalendarEvents.Count, data.WeatherForecasts.Count, data.RssFeedEntries.Count);

        return data;
    }

    private async Task<T?> SafeFetchAsync<T>(Func<Task<Result<T, string>>> fetch) where T : class
    {
        try
        {
            var result = await fetch();
            if (result.IsFailure)
            {
                logger.LogWarning("Provider fetch failed: {Error}", result.Error);
                return null;
            }
            return result.Value;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            logger.LogWarning("Provider fetch timed out: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provider fetch failed: {Message}", ex.Message);
            return null;
        }
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
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var w in widgetsArray.EnumerateArray())
            {
                var id = w.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var type = w.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
                {
                    continue;
                }

                // Skip duplicate IDs
                if (!seenIds.Add(id))
                {
                    logger.LogWarning("AI generated duplicate widget ID '{Id}', skipping", id);
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
            or "todo" or "rss-feed" or "graph" or "app-icon";

    /// <summary>
    /// Programmatic fallback: removes AI-generated widgets that overlap pinned widgets
    /// or each other. Widgets are processed in order; later widgets that conflict are dropped.
    /// </summary>
    private static List<WidgetConfig> RemoveOverlappingWidgets(
        List<WidgetConfig> pinnedWidgets,
        List<WidgetConfig> generatedWidgets,
        int gridCols,
        int gridRows)
    {
        var grid = new bool[gridCols, gridRows];

        // Mark pinned widget cells
        foreach (var w in pinnedWidgets)
        {
            MarkCells(grid, w.Position, gridCols, gridRows);
        }

        var result = new List<WidgetConfig>();
        foreach (var w in generatedWidgets)
        {
            if (CanPlace(grid, w.Position, gridCols, gridRows))
            {
                MarkCells(grid, w.Position, gridCols, gridRows);
                result.Add(w);
            }
        }

        return result;
    }

    private static bool CanPlace(bool[,] grid, WidgetPosition pos, int gridCols, int gridRows)
    {
        if (pos.X + pos.W > gridCols || pos.Y + pos.H > gridRows)
        {
            return false;
        }

        for (var row = pos.Y; row < pos.Y + pos.H; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W; col++)
            {
                if (grid[col, row])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void MarkCells(bool[,] grid, WidgetPosition pos, int gridCols, int gridRows)
    {
        for (var row = pos.Y; row < pos.Y + pos.H && row < gridRows; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W && col < gridCols; col++)
            {
                grid[col, row] = true;
            }
        }
    }

    /// <summary>
    /// Post-parse validation: drops widgets with invalid/missing entity IDs or empty content,
    /// repairs header titles, and shrinks oversized list widgets to fit their actual data.
    /// </summary>
    private List<WidgetConfig> ValidateAndRepairWidgets(
        List<WidgetConfig> widgets,
        AiDataSnapshot aiData,
        Dashboard dashboard)
    {
        var result = new List<WidgetConfig>();
        var initialCount = widgets.Count;

        foreach (var widget in widgets)
        {
            var repaired = ValidateWidget(widget, aiData, dashboard);
            if (repaired != null)
            {
                result.Add(repaired);
            }
        }

        if (result.Count < initialCount)
        {
            logger.LogInformation(
                "Widget validation removed {Removed} of {Total} widgets for dashboard {DashboardId}",
                initialCount - result.Count, initialCount, dashboard.Id);
        }

        return result;
    }

    private WidgetConfig? ValidateWidget(WidgetConfig widget, AiDataSnapshot aiData, Dashboard dashboard)
    {
        var config = widget.Config;

        switch (widget.Type)
        {
            case "calendar":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping calendar widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.CalendarEvents.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping calendar widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                var eventCount = aiData.CalendarEvents[entityId].Count;
                var maxEvents = GetIntProp(config, "maxEvents") ?? eventCount;
                var dataRows = Math.Min(maxEvents, eventCount);
                var idealH = Math.Max(2, 1 + (int)Math.Ceiling(dataRows / 1.0));
                if (widget.Position.H > idealH + 1)
                {
                    widget.Position.H = idealH;
                }
                break;
            }

            case "todo":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping todo widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.TodoItems.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping todo widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                var itemCount = aiData.TodoItems[entityId].Count;
                var idealH = Math.Max(2, 1 + (int)Math.Ceiling(itemCount / 1.0));
                if (widget.Position.H > idealH + 1)
                {
                    widget.Position.H = idealH;
                }
                break;
            }

            case "weather":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping weather widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.EntityStates.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping weather widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "weather-forecast":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping weather-forecast widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.WeatherForecasts.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping weather-forecast widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                break;
            }

            case "rss-feed":
            {
                var entityId = GetStringProp(config, "entityId");
                if (string.IsNullOrEmpty(entityId))
                {
                    logger.LogWarning("Dropping rss-feed widget '{Id}': missing entityId", widget.Id);
                    return null;
                }
                if (!aiData.RssFeedEntries.ContainsKey(entityId))
                {
                    logger.LogWarning("Dropping rss-feed widget '{Id}': entityId '{EntityId}' not in available data", widget.Id, entityId);
                    return null;
                }
                var entryCount = aiData.RssFeedEntries[entityId].Count;
                var idealH = Math.Max(2, 1 + (int)Math.Ceiling(entryCount / 1.0));
                if (widget.Position.H > idealH + 1)
                {
                    widget.Position.H = idealH;
                }
                break;
            }

            case "graph":
            {
                if (!config.TryGetProperty("series", out var seriesEl)
                    || seriesEl.ValueKind != JsonValueKind.Array
                    || seriesEl.GetArrayLength() == 0)
                {
                    logger.LogWarning("Dropping graph widget '{Id}': missing or empty series", widget.Id);
                    return null;
                }
                var hasValidSeries = false;
                foreach (var s in seriesEl.EnumerateArray())
                {
                    var eid = s.TryGetProperty("entityId", out var eidEl) ? eidEl.GetString() : null;
                    if (!string.IsNullOrEmpty(eid) && aiData.EntityStates.ContainsKey(eid))
                    {
                        hasValidSeries = true;
                        break;
                    }
                }
                if (!hasValidSeries)
                {
                    logger.LogWarning("Dropping graph widget '{Id}': no series entity IDs match available data", widget.Id);
                    return null;
                }
                break;
            }

            case "markdown":
            {
                var content = GetStringProp(config, "content");
                if (string.IsNullOrWhiteSpace(content))
                {
                    logger.LogWarning("Dropping markdown widget '{Id}': empty content", widget.Id);
                    return null;
                }
                break;
            }

            case "header":
            {
                var title = GetStringProp(config, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    logger.LogInformation("Repairing header widget '{Id}': setting title to dashboard name", widget.Id);
                    widget.Config = JsonSerializer.SerializeToElement(
                        PatchJsonObject(config, "title", dashboard.Name));
                }
                break;
            }

            // app-icon: no required config — always valid
        }

        return widget;
    }

    /// <summary>
    /// Creates a dictionary from a JsonElement object, adds/replaces a string property, and returns it
    /// ready for serialization. Preserves all existing properties.
    /// </summary>
    private static Dictionary<string, object?> PatchJsonObject(JsonElement original, string key, string value)
    {
        var dict = new Dictionary<string, object?>();
        if (original.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.Name == key) continue;
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.Clone()
                };
            }
        }
        dict[key] = value;
        return dict;
    }

    private static string? GetStringProp(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var p)
        && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static int? GetIntProp(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var p)
        && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : null;

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
