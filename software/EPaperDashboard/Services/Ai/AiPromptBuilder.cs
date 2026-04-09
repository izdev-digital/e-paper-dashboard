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

    private static string BuildSystemPrompt(Dashboard dashboard, LayoutConfig layoutConfig)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an e-paper dashboard designer. Your job is to create a widget layout for an e-paper display.");
        sb.AppendLine("You MUST respond with valid JSON only. No markdown, no explanation, no code fences.");
        sb.AppendLine();

        // Display constraints
        var (width, height) = dashboard.GetEffectiveSize();
        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;

        sb.AppendLine("## Display Constraints");
        sb.AppendLine($"- Resolution: {width}×{height} pixels");
        sb.AppendLine($"- Grid: {gridCols} columns × {gridRows} rows");
        sb.AppendLine("- Color palette: 3 colors only (black, white, red)");
        sb.AppendLine("- E-paper: no animations, no gradients, high contrast required");
        sb.AppendLine();

        // Color scheme
        sb.AppendLine("## Color Scheme");
        sb.AppendLine($"- Background: {layoutConfig.ColorScheme.Background}");
        sb.AppendLine($"- Canvas background: {layoutConfig.ColorScheme.CanvasBackgroundColor}");
        sb.AppendLine($"- Widget background: {layoutConfig.ColorScheme.WidgetBackgroundColor}");
        sb.AppendLine($"- Widget border: {layoutConfig.ColorScheme.WidgetBorderColor}");
        sb.AppendLine($"- Title text: {layoutConfig.ColorScheme.WidgetTitleTextColor}");
        sb.AppendLine($"- Body text: {layoutConfig.ColorScheme.WidgetTextColor}");
        sb.AppendLine($"- Icon color: {layoutConfig.ColorScheme.IconColor}");
        sb.AppendLine($"- Accent: {layoutConfig.ColorScheme.Accent}");
        sb.AppendLine($"- Allowed palette: [{string.Join(", ", layoutConfig.ColorScheme.Palette.Select(p => $"\"{p}\""))}]");
        sb.AppendLine();

        // Reserved cells from pinned widgets
        var pinnedWidgets = layoutConfig.Widgets;
        if (pinnedWidgets.Count > 0)
        {
            sb.AppendLine("## Reserved Cells (user-pinned widgets — do NOT overlap these)");
            foreach (var w in pinnedWidgets)
            {
                sb.AppendLine($"- Widget \"{w.Id}\" (type: {w.Type}): position x={w.Position.X}, y={w.Position.Y}, w={w.Position.W}, h={w.Position.H}");
            }
            sb.AppendLine();

            sb.AppendLine("## Available Grid Area");
            sb.AppendLine($"Place your widgets in the remaining cells of the {gridCols}×{gridRows} grid that are NOT occupied by the reserved widgets above.");
        }
        else
        {
            sb.AppendLine("## Available Grid Area");
            sb.AppendLine($"The entire {gridCols}×{gridRows} grid is available for your layout.");
        }
        sb.AppendLine();

        // Widget type schemas
        sb.AppendLine("## Available Widget Types");
        sb.AppendLine();
        AppendWidgetSchemas(sb);

        // Response format
        sb.AppendLine("## Response Format");
        sb.AppendLine("Respond with a JSON object containing a single \"widgets\" array:");
        sb.AppendLine("```");
        sb.AppendLine("{");
        sb.AppendLine("  \"widgets\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"id\": \"unique-string-id\",");
        sb.AppendLine("      \"type\": \"widget-type-name\",");
        sb.AppendLine("      \"position\": { \"x\": 0, \"y\": 0, \"w\": 6, \"h\": 4 },");
        sb.AppendLine("      \"config\": { ... widget-specific config ... },");
        sb.AppendLine("      \"titleOverride\": \"Optional custom title\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Widget positions must not overlap each other or reserved cells");
        sb.AppendLine($"- x + w must be <= {gridCols}, y + h must be <= {gridRows}");
        sb.AppendLine("- Each widget id must be unique (use descriptive names like \"weather-main\", \"calendar-today\")");
        sb.AppendLine("- Only use widget types from the list above");
        sb.AppendLine("- Only use entity IDs from the available data provided");
        sb.AppendLine("- Use the markdown widget for AI-generated text content (summaries, advice, quotes)");
        sb.AppendLine("- Prefer larger widgets for readability on e-paper");
        sb.AppendLine("- Leave no empty space if possible — fill the grid");

        return sb.ToString();
    }

    private static void AppendWidgetSchemas(StringBuilder sb)
    {
        sb.AppendLine("### header");
        sb.AppendLine("Displays a title with optional badge icons showing entity states.");
        sb.AppendLine("Config: { \"title\": \"string\", \"showClock\": true/false, \"badges\": [{ \"entityId\": \"sensor.xxx\", \"icon\": \"fa-icon-name\" }] }");
        sb.AppendLine("Good for: Top-of-dashboard title bar with at-a-glance sensor values.");
        sb.AppendLine();

        sb.AppendLine("### markdown");
        sb.AppendLine("Renders markdown text content.");
        sb.AppendLine("Config: { \"content\": \"Markdown text here. **Bold**, *italic*, lists, etc.\" }");
        sb.AppendLine("Good for: AI-generated summaries, advice, quotes, notes, any text content.");
        sb.AppendLine();

        sb.AppendLine("### calendar");
        sb.AppendLine("Shows upcoming calendar events from a calendar entity.");
        sb.AppendLine("Config: { \"entityId\": \"calendar.xxx\", \"maxEvents\": 5 }");
        sb.AppendLine("Good for: Today's schedule, upcoming events.");
        sb.AppendLine();

        sb.AppendLine("### weather");
        sb.AppendLine("Shows current weather conditions from a weather entity.");
        sb.AppendLine("Config: { \"entityId\": \"weather.xxx\" }");
        sb.AppendLine("Good for: Current temperature, conditions, humidity, wind.");
        sb.AppendLine();

        sb.AppendLine("### weather-forecast");
        sb.AppendLine("Shows weather forecast (hourly or daily) from a weather entity.");
        sb.AppendLine("Config: { \"entityId\": \"weather.xxx\", \"forecastMode\": \"daily\" or \"hourly\" }");
        sb.AppendLine("Good for: Multi-day or hourly forecast grid.");
        sb.AppendLine();

        sb.AppendLine("### todo");
        sb.AppendLine("Shows a to-do/task list from a todo entity.");
        sb.AppendLine("Config: { \"entityId\": \"todo.xxx\" }");
        sb.AppendLine("Good for: Shopping lists, task lists.");
        sb.AppendLine();

        sb.AppendLine("### rss-feed");
        sb.AppendLine("Shows RSS feed entries from a feedreader entity.");
        sb.AppendLine("Config: { \"entityId\": \"sensor.xxx_feed\" }");
        sb.AppendLine("Good for: News headlines, blog updates.");
        sb.AppendLine();

        sb.AppendLine("### graph");
        sb.AppendLine("Shows a line/bar chart of entity history data.");
        sb.AppendLine("Config: { \"series\": [{ \"entityId\": \"sensor.xxx\", \"color\": \"#000000\" }], \"period\": \"24h\" }");
        sb.AppendLine("Periods: \"1h\", \"6h\", \"24h\", \"7d\", \"30d\"");
        sb.AppendLine("Good for: Temperature trends, energy usage over time.");
        sb.AppendLine();

        sb.AppendLine("### app-icon");
        sb.AppendLine("Displays a FontAwesome icon.");
        sb.AppendLine("Config: { \"icon\": \"fa-icon-name\" }");
        sb.AppendLine("Good for: Decorative icons, visual separators.");
        sb.AppendLine();
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

        // Time context
        var now = DateTimeOffset.Now;
        sb.AppendLine($"Current date/time: {now:dddd, MMMM d, yyyy h:mm tt}");
        sb.AppendLine();

        // User's prompt
        sb.AppendLine("## User Request");
        sb.AppendLine(dashboard.AiPrompt ?? "Create a useful dashboard with the available data.");
        sb.AppendLine();

        // Available data
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
