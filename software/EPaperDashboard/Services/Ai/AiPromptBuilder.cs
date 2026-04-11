using System.Text;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Ai;

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
        var systemPrompt = BuildSystemPrompt(layoutConfig);
        var userPrompt = BuildUserPrompt(
            dashboard, layoutConfig, entityStates, todoItems, calendarEvents, weatherForecasts, rssFeedEntries);

        return (systemPrompt, userPrompt);
    }

    private static string BuildSystemPrompt(LayoutConfig layoutConfig)
    {
        var gridCols = layoutConfig.GridCols > 0 ? layoutConfig.GridCols : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;
        var cs = layoutConfig.ColorScheme;
        var paletteStr = string.Join(", ", cs.Palette.Select(p => $"\"{p}\""));

        return $$"""
            You are an e-paper dashboard content planner. Your job is to decide WHAT content to show on a dashboard.
            The server will handle widget sizing and placement — you do NOT need to specify positions or sizes.
            You MUST respond with valid JSON only. No markdown, no explanation, no code fences.

            ## Display Info
            - Grid: {{gridCols}} columns × {{gridRows}} rows (limited space — be selective)
            - Color palette: [{{paletteStr}}]
            - E-paper: no animations, no gradients, high contrast

            ## Available Widget Types

            ### header
            A title bar with optional sensor badges.
            Config: {"title": "Dashboard Title" (REQUIRED), "badges": [{"entityId": "sensor.xxx", "icon": "fa-thermometer-half"}] (optional)}
            Data: Badge entityId must be a sensor/binary_sensor from Available Data. Shows state + unit.

            ### markdown
            Free-form text rendered as markdown.
            Config: {"content": "Your text here" (REQUIRED)}
            Supported syntax: headings (#-####), **bold**, *italic*, ~~strikethrough~~, lists (-, 1., nested), task lists (- [ ], - [x]), blockquotes (>), horizontal rules (---), fenced code blocks (```).
            Inline Font Awesome solid icons: use :fa-icon-name: syntax (e.g. :fa-sun: :fa-house: :fa-calendar: :fa-check:).
            Images (![alt](url)) are NOT supported — use :fa-icon: icons for visual elements instead.
            HTML tags are NOT supported and will be stripped — use only markdown syntax.
            Content MUST NOT be empty — write meaningful, concise text. Good for summaries, greetings, quotes, advice.

            ### calendar
            Upcoming events from a calendar entity. Shows one line per event.
            Config: {"entityId": "calendar.xxx" (REQUIRED), "maxEvents": 5 (optional, default 7)}

            ### weather
            Current weather conditions in a compact 2×2 layout (temperature, condition, pressure, humidity).
            Config: {"entityId": "weather.xxx" (REQUIRED)}

            ### weather-forecast
            Multi-column daily or hourly forecast showing time, condition, high/low temps.
            Config: {"entityId": "weather.xxx" (REQUIRED), "forecastMode": "daily" or "hourly" (optional, default "daily")}

            ### todo
            Task list with status icons. Shows pending tasks first, then completed.
            Config: {"entityId": "todo.xxx" (REQUIRED), "showCompleted": true/false (optional, default true), "maxItems": 10 (optional, default 50)}

            ### rss-feed
            Shows the first RSS entry headline with a QR code link. Only ONE entry, not a list.
            Config: {"entityId": "sensor.xxx_feed" (REQUIRED)}

            ### graph
            Line or bar chart of sensor history.
            Config: {"series": [{"entityId": "sensor.xxx", "color": "#000000"}] (REQUIRED), "period": "24h" (REQUIRED, one of "1h","6h","24h","7d","30d"), "plotType": "line" or "bar" (optional, default "line")}
            Series colors must be from the allowed palette.

            ### app-icon
            EPaperDashboard app logo. Purely decorative.
            Config: {}

            ## Response Format
            Return a JSON object with a "widgets" array. List widgets in PRIORITY ORDER (most important first).
            The server will place them in this order — lower-priority widgets may be dropped if the grid is full.

            {"widgets": [{"type": "widget-type", "config": {...}, "titleOverride": "Optional title"}]}

            ## Rules
            - List widgets in priority order (most important first)
            - Only use entity IDs that appear in the Available Data section
            - Every widget MUST include all REQUIRED config fields for its type
            - Markdown content MUST be non-empty meaningful text
            - Be selective — a focused dashboard is better than a cluttered one
            - Use markdown widgets for AI-generated text (summaries, advice, greetings)
            - Do NOT include position or size — the server handles layout
            - Do NOT include a header widget if one already exists on the dashboard
            - Do NOT include an app-icon widget unless the user explicitly requests it
            """;
    }

    private static string BuildUserPrompt(
        Dashboard dashboard,
        LayoutConfig layoutConfig,
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

        // Tell the AI if a header widget already exists so it doesn't add another
        if (layoutConfig.Widgets?.Any(w => string.Equals(w.Type, "header", StringComparison.OrdinalIgnoreCase)) == true)
        {
            sb.AppendLine("NOTE: A header widget already exists on this dashboard. Do NOT add another header.");
            sb.AppendLine();
        }

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
                foreach (var entry in forecast.Take(5))
                {
                    if (entry is System.Text.Json.JsonElement je)
                    {
                        var parts = new List<string>();
                        if (je.TryGetProperty("datetime", out var dt)) parts.Add(dt.GetString() ?? "");
                        if (je.TryGetProperty("condition", out var cond)) parts.Add(cond.GetString() ?? "");
                        if (je.TryGetProperty("temperature", out var temp)) parts.Add($"{temp}°");
                        if (je.TryGetProperty("templow", out var tempLow)) parts.Add($"low {tempLow}°");
                        if (je.TryGetProperty("precipitation_probability", out var precip)) parts.Add($"{precip}% precip");
                        if (je.TryGetProperty("wind_speed", out var wind)) parts.Add($"wind {wind}");
                        if (parts.Count > 0)
                            sb.AppendLine($"  - {string.Join(", ", parts)}");
                    }
                }
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
