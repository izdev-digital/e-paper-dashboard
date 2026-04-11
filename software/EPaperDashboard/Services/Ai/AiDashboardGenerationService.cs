using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Services.Providers;

namespace EPaperDashboard.Services.Ai;

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

        var aiServiceResult = aiServiceFactory.Create(user.AiConfig, dashboard.Id.ToString());
        if (aiServiceResult.IsFailure)
        {
            return StoreError(dashboard, aiServiceResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(promptOverride))
        {
            dashboard.AiPrompt = promptOverride;
        }

        var aiService = aiServiceResult.Value;

        var aiData = await FetchDataForAi(dashboard);

        var dataSummary = new AiDataSummary
        {
            EntityStates = aiData.EntityStates.Count,
            TodoLists = [.. aiData.TodoItems.Keys],
            Calendars = [.. aiData.CalendarEvents.Keys],
            WeatherEntities = [.. aiData.WeatherForecasts.Keys],
            RssFeeds = [.. aiData.RssFeedEntries.Keys]
        };

        var layoutConfig = dashboard.LayoutConfig ?? CreateDefaultLayoutConfig(dashboard);

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

        var completionResult = await aiService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);
        if (completionResult.IsFailure)
        {
            return StoreError(dashboard, completionResult.Error);
        }

        var parseResult = ParseAiResponse(completionResult.Value);
        if (parseResult.IsFailure)
        {
            logger.LogWarning(
                "AI response parsing failed: {Error}. Running JSON repair pass. Raw response: {Response}",
                parseResult.Error, completionResult.Value);

            var repairResult = await RepairJsonAsync(
                aiService, completionResult.Value, parseResult.Error, cancellationToken);
            if (repairResult.IsFailure)
            {
                return StoreError(dashboard, $"AI returned invalid JSON and repair failed: {repairResult.Error}");
            }

            parseResult = repairResult;
        }

        var validatedWidgets = ValidateAndRepairWidgets(parseResult.Value, aiData, dashboard);
        if (validatedWidgets.Count == 0)
        {
            return StoreError(dashboard, "All AI-generated widgets were invalid after validation");
        }

        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;

        ComputeWidgetSizes(validatedWidgets, aiData, layoutConfig, gridCols);
        var placedWidgets = PackWidgets(validatedWidgets, layoutConfig.Widgets, gridCols, gridRows);

        if (placedWidgets.Count == 0)
        {
            return StoreError(dashboard, "No widgets could be placed on the grid");
        }

        dashboard.AiGeneratedWidgets = placedWidgets;
        dashboard.LastAiGenerationTime = DateTimeOffset.UtcNow;
        dashboard.LastAiGenerationError = null;
        dashboardService.UpdateDashboard(dashboard);

        logger.LogInformation(
            "AI generated {WidgetCount} widgets for dashboard {DashboardId} ({Placed} placed on grid)",
            validatedWidgets.Count, dashboard.Id, placedWidgets.Count);

        return new AiGenerationResult
        {
            Widgets = placedWidgets,
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
            {
                data.EntityStates[state.EntityId] = state;
            }
        }

        var todoItems = await todoTask;
        if (todoItems != null)
        {
            data.TodoItems = todoItems;
        }

        var calendarEvents = await calendarTask;
        if (calendarEvents != null)
        {
            data.CalendarEvents = calendarEvents;
        }

        var weatherForecasts = await weatherTask;
        if (weatherForecasts != null)
        {
            data.WeatherForecasts = weatherForecasts;
        }

        var rssEntries = await rssTask;
        if (rssEntries != null)
        {
            data.RssFeedEntries = rssEntries;
        }

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

    private async Task<Result<List<WidgetConfig>, string>> RepairJsonAsync(
        IAiService aiService,
        string brokenResponse,
        string parseError,
        CancellationToken cancellationToken)
    {
        const string repairSystemPrompt = """
            You are a JSON repair tool. The user will give you a broken JSON response and the parse error.
            Fix the JSON so it is valid. Return ONLY the corrected JSON — no markdown, no explanation, no code fences.
            The JSON must be an object with a "widgets" array: {"widgets": [...]}
            Do NOT change the meaning of the data — only fix syntax errors (missing commas, brackets, quotes, trailing commas, etc.).
            """;

        var repairUserPrompt = $"""
            ## Parse Error
            {parseError}

            ## Broken JSON
            {brokenResponse}
            """;

        var repairResult = await aiService.GenerateCompletionAsync(
            repairSystemPrompt, repairUserPrompt, cancellationToken);

        if (repairResult.IsFailure)
        {
            return $"Repair LLM call failed: {repairResult.Error}";
        }

        var repairedParseResult = ParseAiResponse(repairResult.Value);
        if (repairedParseResult.IsFailure)
        {
            return $"Repaired JSON still invalid: {repairedParseResult.Error}";
        }

        logger.LogInformation("JSON repair pass succeeded, recovered {Count} widgets", repairedParseResult.Value.Count);
        return repairedParseResult;
    }


    private Result<List<WidgetConfig>, string> ParseAiResponse(string response)
    {
        try
        {
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
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var w in widgetsArray.EnumerateArray())
            {
                var type = w.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;

                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                if (!IsKnownWidgetType(type))
                {
                    logger.LogWarning("AI generated unknown widget type '{Type}', skipping", type);
                    continue;
                }

                typeCounts.TryGetValue(type, out var count);
                typeCounts[type] = count + 1;
                var id = count == 0 ? type : $"{type}-{count + 1}";

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
                    Position = new WidgetPosition(),
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
            or "todo" or "rss-feed" or "graph" or "app-icon" or "ai-content";

    private void ComputeWidgetSizes(
        List<WidgetConfig> widgets,
        AiDataSnapshot aiData,
        LayoutConfig layoutConfig,
        int gridCols)
    {
        var metrics = ComputeLayoutMetrics(layoutConfig, gridCols);

        foreach (var widget in widgets)
        {
            var (w, h) = ComputeIdealSize(widget, aiData, metrics, gridCols);
            widget.Position.W = w;
            widget.Position.H = h;
        }
    }

    private static (int cellWidth, int cellHeight, int charsPerCellWidth, double linesPerCell, int textLineHeight, int titleLineHeight)
        ComputeLayoutMetrics(LayoutConfig layoutConfig, int gridCols)
    {
        var (width, height) = (layoutConfig.Width, layoutConfig.Height);
        if (width <= 0) width = 800;
        if (height <= 0) height = 480;

        var canvasPadding = layoutConfig.CanvasPadding > 0 ? layoutConfig.CanvasPadding : 8;
        var widgetGap = layoutConfig.WidgetGap > 0 ? layoutConfig.WidgetGap : 8;
        var widgetPadding = layoutConfig.WidgetPadding > 0 ? layoutConfig.WidgetPadding : 8;
        var widgetBorder = layoutConfig.WidgetBorder >= 0 ? layoutConfig.WidgetBorder : 1;
        var titleFontSize = layoutConfig.TitleFontSize > 0 ? layoutConfig.TitleFontSize : 14;
        var textFontSize = layoutConfig.TextFontSize > 0 ? layoutConfig.TextFontSize : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;

        var usableWidth = width - (2 * canvasPadding) - ((gridCols - 1) * widgetGap);
        var usableHeight = height - (2 * canvasPadding) - ((gridRows - 1) * widgetGap);
        var cellWidth = usableWidth / gridCols;
        var cellHeight = usableHeight / gridRows;

        var innerPadding = 2 * (widgetPadding + widgetBorder);
        var titleLineHeight = (int)(titleFontSize * 1.4);
        var textLineHeight = (int)(textFontSize * 1.4);
        var charsPerCellWidth = (int)((cellWidth - innerPadding) / (textFontSize * 0.55));
        var linesPerCell = (double)(cellHeight - innerPadding) / textLineHeight;

        return (cellWidth, cellHeight, Math.Max(1, charsPerCellWidth), Math.Max(1, linesPerCell), textLineHeight, titleLineHeight);
    }

    private static (int w, int h) ComputeIdealSize(
        WidgetConfig widget,
        AiDataSnapshot aiData,
        (int cellWidth, int cellHeight, int charsPerCellWidth, double linesPerCell, int textLineHeight, int titleLineHeight) metrics,
        int gridCols)
    {
        var config = widget.Config;

        switch (widget.Type)
        {
            case "header":
                return (gridCols, 1);

            case "app-icon":
                return (1, 1);

            case "weather":
                return (Math.Min(4, gridCols), 2);

            case "weather-forecast":
            {
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                return (w, 3);
            }

            case "calendar":
            {
                var entityId = GetStringProp(config, "entityId") ?? "";
                var eventCount = aiData.CalendarEvents.TryGetValue(entityId, out var events) ? events.Count : 0;
                var maxEvents = GetIntProp(config, "maxEvents") ?? 7;
                var dataRows = Math.Min(maxEvents, eventCount);
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                var contentLines = dataRows;
                var h = 1 + (int)Math.Ceiling(contentLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            case "todo":
            {
                var entityId = GetStringProp(config, "entityId") ?? "";
                var itemCount = aiData.TodoItems.TryGetValue(entityId, out var items) ? items.Count : 0;
                var maxItems = GetIntProp(config, "maxItems") ?? 50;
                var dataRows = Math.Min(maxItems, itemCount);
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                var contentLines = (int)Math.Ceiling(dataRows * 1.3);
                var h = 1 + (int)Math.Ceiling(contentLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            case "rss-feed":
            {
                var w = Math.Min(4, Math.Max(3, gridCols / 3));
                return (w, 4);
            }

            case "graph":
            {
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                return (w, 3);
            }

            case "markdown":
            case "ai-content":
            {
                var content = GetStringProp(config, "content") ?? "";
                var charCount = content.Length;
                int w;
                if (charCount < 100)
                {
                    w = Math.Min(gridCols, Math.Max(3, gridCols / 3));
                }
                else if (charCount < 300)
                {
                    w = Math.Min(gridCols, Math.Max(4, gridCols / 2));
                }
                else
                {
                    w = Math.Min(gridCols, Math.Max(6, gridCols * 2 / 3));
                }

                var charsPerLine = metrics.charsPerCellWidth * w;
                var textLines = (int)Math.Ceiling((double)charCount / charsPerLine);
                var newlineCount = content.Count(c => c == '\n');
                textLines = Math.Max(textLines, newlineCount + 1);
                var h = 1 + (int)Math.Ceiling(textLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            default:
                return (Math.Min(4, gridCols), 2);
        }
    }

    private List<WidgetConfig> PackWidgets(
        List<WidgetConfig> widgets,
        List<WidgetConfig> pinnedWidgets,
        int gridCols,
        int gridRows)
    {
        var grid = new bool[gridCols, gridRows];

        foreach (var pinned in pinnedWidgets)
        {
            MarkCells(grid, pinned.Position, gridCols, gridRows);
        }

        var placed = new List<WidgetConfig>();

        foreach (var widget in widgets)
        {
            var idealW = widget.Position.W;
            var idealH = widget.Position.H;

            if (TryPlace(grid, widget, idealW, idealH, gridCols, gridRows))
            {
                placed.Add(widget);
                continue;
            }

            var placed2 = false;
            for (var h = idealH - 1; h >= 1; h--)
            {
                if (TryPlace(grid, widget, idealW, h, gridCols, gridRows))
                {
                    placed2 = true;
                    placed.Add(widget);
                    break;
                }
            }
            if (placed2)
            {
                continue;
            }

            for (var w = idealW - 1; w >= 1; w--)
            {
                for (var h = idealH; h >= 1; h--)
                {
                    if (TryPlace(grid, widget, w, h, gridCols, gridRows))
                    {
                        placed2 = true;
                        placed.Add(widget);
                        break;
                    }
                }
                if (placed2)
                {
                    break;
                }
            }

            if (!placed2)
            {
                logger.LogInformation(
                    "Widget '{Id}' ({Type}, {W}×{H}) could not fit on the grid, skipping",
                    widget.Id, widget.Type, idealW, idealH);
            }
        }

        return placed;
    }

    private static bool TryPlace(
        bool[,] grid, WidgetConfig widget,
        int w, int h,
        int gridCols, int gridRows)
    {
        for (var row = 0; row <= gridRows - h; row++)
        {
            for (var col = 0; col <= gridCols - w; col++)
            {
                var pos = new WidgetPosition { X = col, Y = row, W = w, H = h };
                if (CanPlace(grid, pos, gridCols, gridRows))
                {
                    widget.Position = pos;
                    MarkCells(grid, pos, gridCols, gridRows);
                    return true;
                }
            }
        }
        return false;
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
        }

        return widget;
    }

    private static Dictionary<string, object?> PatchJsonObject(JsonElement original, string key, string value)
    {
        var dict = new Dictionary<string, object?>();
        if (original.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.Name == key)
                {
                    continue;
                }
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

    private static LayoutConfig CreateDefaultLayoutConfig(Dashboard dashboard)
    {
        var (width, height) = dashboard.GetEffectiveSize();
        return new LayoutConfig
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
