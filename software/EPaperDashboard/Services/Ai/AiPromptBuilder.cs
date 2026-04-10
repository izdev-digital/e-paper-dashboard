using System.Text;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Ai;

/// <summary>
/// Builds system and user prompts for AI dashboard generation.
/// Includes widget type schemas, grid constraints, color palette,
/// available entity data, and reserved widget positions.
/// </summary>
public sealed class AiPromptBuilder
{
    public (string systemPrompt, string userPrompt) BuildPrompt(
        Dashboard dashboard,
        LayoutConfig layoutConfig,
        Dictionary<string, HassEntityState> entityStates,
        Dictionary<string, List<TodoItem>> todoItems,
        Dictionary<string, List<CalendarEvent>> calendarEvents,
        Dictionary<string, List<object?>> weatherForecasts,
        Dictionary<string, List<RssFeedEntry>> rssFeedEntries)
    {
        var systemPrompt = BuildSystemPrompt(dashboard, layoutConfig);
        var userPrompt = BuildUserPrompt(
            dashboard, entityStates, todoItems, calendarEvents, weatherForecasts, rssFeedEntries);

        return (systemPrompt, userPrompt);
    }

    public string BuildVerificationPrompt(
        List<WidgetConfig> pinnedWidgets,
        List<WidgetConfig> generatedWidgets,
        int gridCols,
        int gridRows)
    {
        var pinnedSection = pinnedWidgets.Count > 0
            ? $$"""

              ## Pinned Widgets (immutable — cannot be moved or removed)
              {{FormatWidgetList(pinnedWidgets)}}

              """
            : "";

        var occupancyGrid = BuildOccupancyGrid(pinnedWidgets, generatedWidgets, gridCols, gridRows);

        return $$"""
            You are a layout validator for an e-paper dashboard grid system.
            You MUST respond with valid JSON only. No markdown, no explanation, no code fences.

            Grid size: {{gridCols}} columns × {{gridRows}} rows.
            Constraints: x + w <= gridCols, y + h <= gridRows. No two widgets may share any cell.
            {{pinnedSection}}
            ## AI-Generated Widgets (you may reposition, resize, or remove these to fix overlaps)
            {{FormatWidgetList(generatedWidgets)}}

            ## Current Occupancy Grid (P=pinned, A=AI-generated, X=overlap conflict, .=empty)
            ```
            {{occupancyGrid}}```

            ## Task
            Check the layout above for:
            1. Overlaps between any widgets (pinned or AI-generated)
            2. Widgets exceeding grid bounds
            3. AI widgets that overlap pinned widgets

            If there are NO issues, return the AI-generated widgets unchanged.
            If there ARE issues, fix them by repositioning, resizing, or removing AI-generated widgets only.
            Never modify pinned widgets.

            Return the corrected AI-generated widgets as:
            {"widgets": [{"id": "...", "type": "...", "position": {"x": 0, "y": 0, "w": 6, "h": 4}, "config": {...}, "titleOverride": "..."}]}
            """;
    }

    public static bool HasOverlaps(
        List<WidgetConfig> pinnedWidgets,
        List<WidgetConfig> generatedWidgets,
        int gridCols,
        int gridRows)
    {
        var grid = new bool[gridCols, gridRows];

        foreach (var w in pinnedWidgets)
        {
            if (!MarkCells(grid, w.Position, gridCols, gridRows))
            {
                return true;
            }
        }

        foreach (var w in generatedWidgets)
        {
            if (!MarkCells(grid, w.Position, gridCols, gridRows))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MarkCells(bool[,] grid, WidgetPosition pos, int gridCols, int gridRows)
    {
        for (var row = pos.Y; row < pos.Y + pos.H && row < gridRows; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W && col < gridCols; col++)
            {
                if (grid[col, row])
                {
                    return false;
                }
                grid[col, row] = true;
            }
        }
        return true;
    }

    private static string BuildOccupancyGrid(
        List<WidgetConfig> pinnedWidgets,
        List<WidgetConfig> generatedWidgets,
        int gridCols,
        int gridRows)
    {
        var grid = new int[gridCols, gridRows];

        foreach (var w in pinnedWidgets)
        {
            FillGrid(grid, w.Position, 1, gridCols, gridRows);
        }

        foreach (var w in generatedWidgets)
        {
            FillGrid(grid, w.Position, 2, gridCols, gridRows);
        }

        var sb = new StringBuilder();
        sb.Append("   ");
        for (var c = 0; c < gridCols; c++)
        {
            sb.Append($"{c,2}");
        }
        sb.AppendLine();

        for (var r = 0; r < gridRows; r++)
        {
            sb.Append($"{r,2} ");
            for (var c = 0; c < gridCols; c++)
            {
                var ch = grid[c, r] switch
                {
                    1 => " P",
                    2 => " A",
                    3 => " X",
                    _ => " ."
                };
                sb.Append(ch);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void FillGrid(int[,] grid, WidgetPosition pos, int value, int gridCols, int gridRows)
    {
        for (var row = pos.Y; row < pos.Y + pos.H && row < gridRows; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W && col < gridCols; col++)
            {
                if (grid[col, row] != 0)
                {
                    grid[col, row] = 3;
                }
                else
                {
                    grid[col, row] = value;
                }
            }
        }
    }

    private static string FormatWidgetList(List<WidgetConfig> widgets)
    {
        var sb = new StringBuilder();
        foreach (var w in widgets)
        {
            sb.AppendLine($"""- "{w.Id}" (type: {w.Type}): x={w.Position.X}, y={w.Position.Y}, w={w.Position.W}, h={w.Position.H}""");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildSystemPrompt(Dashboard dashboard, LayoutConfig layoutConfig)
    {
        var (width, height) = dashboard.GetEffectiveSize();
        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;

        var canvasPadding = layoutConfig.CanvasPadding > 0 ? layoutConfig.CanvasPadding : 8;
        var widgetGap = layoutConfig.WidgetGap > 0 ? layoutConfig.WidgetGap : 8;
        var widgetPadding = layoutConfig.WidgetPadding > 0 ? layoutConfig.WidgetPadding : 8;
        var widgetBorder = layoutConfig.WidgetBorder >= 0 ? layoutConfig.WidgetBorder : 1;
        var titleFontSize = layoutConfig.TitleFontSize > 0 ? layoutConfig.TitleFontSize : 14;
        var textFontSize = layoutConfig.TextFontSize > 0 ? layoutConfig.TextFontSize : 12;

        var usableWidth = width - (2 * canvasPadding) - ((gridCols - 1) * widgetGap);
        var usableHeight = height - (2 * canvasPadding) - ((gridRows - 1) * widgetGap);
        var cellWidth = usableWidth / gridCols;
        var cellHeight = usableHeight / gridRows;

        var innerPadding = 2 * (widgetPadding + widgetBorder);
        var titleLineHeight = (int)(titleFontSize * 1.4);
        var textLineHeight = (int)(textFontSize * 1.4);
        var charsPerCellWidth = (int)((cellWidth - innerPadding) / (textFontSize * 0.55));
        var linesPerCell = (cellHeight - innerPadding - titleLineHeight - 8) / textLineHeight;

        var cs = layoutConfig.ColorScheme;
        var paletteStr = string.Join(", ", cs.Palette.Select(p => $"\"{p}\""));

        var pinnedWidgets = layoutConfig.Widgets;
        var reservedSection = pinnedWidgets.Count > 0
            ? BuildReservedSection(pinnedWidgets, gridCols, gridRows)
            : $$"""
              ## Available Grid Area
              The entire {{gridCols}}×{{gridRows}} grid is available for your layout.
              """;

        return $$"""
            You are an e-paper dashboard designer. Your job is to create a widget layout for an e-paper display.
            You MUST respond with valid JSON only. No markdown, no explanation, no code fences.

            ## Display Constraints
            - Resolution: {{width}}×{{height}} pixels
            - Grid: {{gridCols}} columns × {{gridRows}} rows
            - Color palette: 3 colors only (black, white, red)
            - E-paper: no animations, no gradients, high contrast required

            ## Cell & Font Metrics (use these to size widgets correctly)
            - Cell size: ~{{cellWidth}}×{{cellHeight}} pixels per grid cell
            - Widget inner padding: {{widgetPadding}}px each side, border: {{widgetBorder}}px
            - Title font: {{titleFontSize}}px (line height ~{{titleLineHeight}}px) — title row takes ~{{titleLineHeight + 8}}px
            - Body text font: {{textFontSize}}px (line height ~{{textLineHeight}}px)
            - Approx chars per cell width: ~{{charsPerCellWidth}}
            - Usable height per cell: ~{{cellHeight - innerPadding}}px (after padding/border)
            - Text lines that fit in 1 cell height: ~{{linesPerCell}}

            ## Sizing Guidelines
            Size widgets to OPTIMALLY FIT their content — do NOT make them larger than needed.
            - header: w=full grid width, h=1 (title + badges fit in one row)
            - markdown: estimate lines needed = ceil(chars / ({{charsPerCellWidth}} * w)) + title row. Set h accordingly.
            - weather: w=3-4, h=2 (current conditions are compact)
            - weather-forecast: w=4-6, h=2-3 (daily: ~5 columns of icons+temps)
            - calendar: h = min(ceil(events / 1) + 1, available). 1 row title + 1 row per event shown.
            - todo: h = min(ceil(items / 1) + 1, available). 1 row title + 1 row per item.
            - rss-feed: h = min(ceil(entries / 1) + 1, available). 1 row title + 1 row per headline.
            - graph: w=4-6, h=3-4 (needs space for axes and data)
            - app-icon: w=1, h=1 (single icon)
            Prefer COMPACT widgets. Only make a widget large if the content requires it.
            It is BETTER to leave empty grid space than to stretch a widget beyond its content.

            ## Color Scheme
            - Background: {{cs.Background}}
            - Canvas background: {{cs.CanvasBackgroundColor}}
            - Widget background: {{cs.WidgetBackgroundColor}}
            - Widget border: {{cs.WidgetBorderColor}}
            - Title text: {{cs.WidgetTitleTextColor}}
            - Body text: {{cs.WidgetTextColor}}
            - Icon color: {{cs.IconColor}}
            - Accent: {{cs.Accent}}
            - Allowed palette: [{{paletteStr}}]

            {{reservedSection}}

            ## Available Widget Types

            ### header
            Displays a title with optional badge icons showing entity states.
            Config: {"title": "string", "showClock": true/false, "badges": [{"entityId": "sensor.xxx", "icon": "fa-icon-name"}]}
            Good for: Top-of-dashboard title bar with at-a-glance sensor values.
            Sizing: w=full grid width, h=1. Always 1 row tall — title and badges render inline.

            ### markdown
            Renders markdown text content.
            Config: {"content": "Markdown text here. **Bold**, *italic*, lists, etc."}
            Good for: AI-generated summaries, advice, quotes, notes, any text content.
            Sizing: Calculate lines = ceil(character_count / (chars_per_cell_width * w)). Then h = ceil((lines * text_line_height + title_row_height) / cell_height). Keep content concise.

            ### calendar
            Shows upcoming calendar events from a calendar entity.
            Config: {"entityId": "calendar.xxx", "maxEvents": 5}
            Good for: Today's schedule, upcoming events.
            Sizing: h = 1 (title) + ceil(maxEvents * text_line_height / cell_usable_height). For 5 events, typically h=3-4. Set maxEvents based on available space.

            ### weather
            Shows current weather conditions from a weather entity.
            Config: {"entityId": "weather.xxx"}
            Good for: Current temperature, conditions, humidity, wind.
            Sizing: w=3-4, h=2. Compact — shows icon + temp + a few stats.

            ### weather-forecast
            Shows weather forecast (hourly or daily) from a weather entity.
            Config: {"entityId": "weather.xxx", "forecastMode": "daily" or "hourly"}
            Good for: Multi-day or hourly forecast grid.
            Sizing: w=4-6, h=2-3. Each forecast column is ~60px wide. 5 days needs w ≈ ceil(5*60/cell_width).

            ### todo
            Shows a to-do/task list from a todo entity.
            Config: {"entityId": "todo.xxx"}
            Good for: Shopping lists, task lists.
            Sizing: h = 1 (title) + ceil(item_count * text_line_height / cell_usable_height). If many items, cap display and keep h reasonable.

            ### rss-feed
            Shows RSS feed entries from a feedreader entity.
            Config: {"entityId": "sensor.xxx_feed"}
            Good for: News headlines, blog updates.
            Sizing: h = 1 (title) + ceil(entry_count * text_line_height / cell_usable_height). Typically shows 3-5 headlines.

            ### graph
            Shows a line/bar chart of entity history data.
            Config: {"series": [{"entityId": "sensor.xxx", "color": "#000000"}], "period": "24h"}
            Periods: "1h", "6h", "24h", "7d", "30d"
            Good for: Temperature trends, energy usage over time.
            Sizing: w=4-6, h=3-4. Needs room for axes, labels, and data plot.

            ### app-icon
            Displays a FontAwesome icon.
            Config: {"icon": "fa-icon-name"}
            Good for: Decorative icons, visual separators.
            Sizing: w=1, h=1. Single cell is sufficient.

            ## Response Format
            Respond with a JSON object containing a single "widgets" array:
            ```
            {
              "widgets": [
                {
                  "id": "unique-string-id",
                  "type": "widget-type-name",
                  "position": { "x": 0, "y": 0, "w": 6, "h": 4 },
                  "config": { ... widget-specific config ... },
                  "titleOverride": "Optional custom title"
                }
              ]
            }
            ```

            Rules:
            - Widget positions must not overlap each other or reserved cells
            - x + w must be <= {{gridCols}}, y + h must be <= {{gridRows}}
            - Each widget id must be unique (use descriptive names like "weather-main", "calendar-today")
            - Only use widget types from the list above
            - Only use entity IDs from the available data provided
            - Use the markdown widget for AI-generated text content (summaries, advice, quotes)
            - Size each widget to OPTIMALLY FIT its content using the cell & font metrics above
            - Do NOT stretch widgets to fill empty space — compact is better
            - For text widgets (markdown, todo, calendar, rss-feed): calculate the number of text lines, then set h = ceil(lines * lineHeight / cellHeight) + 1 for the title row
            - Empty grid cells are acceptable and preferred over oversized widgets
            """;
    }

    private static string BuildReservedSection(List<WidgetConfig> pinnedWidgets, int gridCols, int gridRows)
    {
        var occupancyGrid = BuildOccupancyGrid(pinnedWidgets, new List<WidgetConfig>(), gridCols, gridRows);

        return $$"""
            ## Reserved Cells (user-pinned widgets — do NOT overlap these)
            {{FormatWidgetList(pinnedWidgets)}}

            ## Occupancy Grid (P=pinned/reserved, .=available)
            ```
            {{occupancyGrid}}```

            ## Available Grid Area
            Place your widgets ONLY in cells marked '.' in the grid above. Do NOT place any widget in cells marked 'P'.
            """;
    }

    private static string BuildUserPrompt(
        Dashboard dashboard,
        Dictionary<string, HassEntityState> entityStates,
        Dictionary<string, List<TodoItem>> todoItems,
        Dictionary<string, List<CalendarEvent>> calendarEvents,
        Dictionary<string, List<object?>> weatherForecasts,
        Dictionary<string, List<RssFeedEntry>> rssFeedEntries)
    {
        var sb = new StringBuilder();
        var now = DateTimeOffset.Now;

        sb.AppendLine($"Current date/time: {now:dddd, MMMM d, yyyy h:mm tt}");
        sb.AppendLine();
        sb.AppendLine("## User Request");
        sb.AppendLine(dashboard.AiPrompt ?? "Create a useful dashboard with the available data.");
        sb.AppendLine();
        sb.AppendLine("## Available Data");
        sb.AppendLine();

        if (entityStates.Count > 0)
        {
            sb.AppendLine("### Entity States");
            foreach (var (entityId, state) in entityStates)
            {
                var friendlyName = state.Attributes.TryGetValue("friendly_name", out var fn)
                    ? fn?.ToString() : null;
                var unit = state.Attributes.TryGetValue("unit_of_measurement", out var u)
                    ? u?.ToString() : null;

                var display = !string.IsNullOrEmpty(friendlyName) ? $"{friendlyName} ({entityId})" : entityId;
                var value = !string.IsNullOrEmpty(unit) ? $"{state.State} {unit}" : state.State;
                sb.AppendLine($"- {display}: {value}");
            }
            sb.AppendLine();
        }

        if (calendarEvents.Count > 0)
        {
            sb.AppendLine("### Calendar Events");
            foreach (var (entityId, events) in calendarEvents)
            {
                sb.AppendLine($"Calendar: {entityId} ({events.Count} events)");
                foreach (var evt in events.Take(10))
                {
                    var time = evt.AllDay ? "All day" : evt.Start;
                    sb.AppendLine($"  - {time}: {evt.Summary}");
                }
            }
            sb.AppendLine();
        }

        if (todoItems.Count > 0)
        {
            sb.AppendLine("### Todo Lists");
            foreach (var (entityId, items) in todoItems)
            {
                sb.AppendLine($"Todo: {entityId} ({items.Count} items)");
                foreach (var item in items.Take(10))
                {
                    sb.AppendLine($"  - [{item.Status}] {item.Summary}");
                }
            }
            sb.AppendLine();
        }

        if (weatherForecasts.Count > 0)
        {
            sb.AppendLine("### Weather Forecasts");
            foreach (var (entityId, forecast) in weatherForecasts)
            {
                sb.AppendLine($"Forecast: {entityId} ({forecast.Count} entries)");
            }
            sb.AppendLine();
        }

        if (rssFeedEntries.Count > 0)
        {
            sb.AppendLine("### RSS Feeds");
            foreach (var (entityId, entries) in rssFeedEntries)
            {
                sb.AppendLine($"Feed: {entityId} ({entries.Count} entries)");
                foreach (var entry in entries.Take(5))
                {
                    sb.AppendLine($"  - {entry.Title}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
