using System.Globalization;
using System.Text;
using System.Text.Json;
using QRCoder;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Renders a dashboard layout as self-contained static HTML/CSS,
/// reusing the same visual structure as the Angular frontend widgets.
/// </summary>
public sealed class DashboardHtmlRenderingService(
    HomeAssistantService homeAssistantService,
    ILogger<DashboardHtmlRenderingService> logger,
    IWebHostEnvironment webHostEnvironment)
{
    private readonly HomeAssistantService _homeAssistantService = homeAssistantService;
    private readonly ILogger<DashboardHtmlRenderingService> _logger = logger;
    private readonly IWebHostEnvironment _env = webHostEnvironment;
    private string? _cachedWidgetCss;

    // =============================================
    // LAYOUT MODEL (mirrors Angular TS types)
    // =============================================

    public record LayoutConfig(
        int Width,
        int Height,
        int GridCols,
        int GridRows,
        ColorSchemeConfig ColorScheme,
        List<WidgetConfigEntry> Widgets,
        int CanvasPadding,
        int WidgetGap,
        int WidgetBorder,
        int TitleFontSize,
        int TextFontSize);

    public record ColorSchemeConfig(
        string Name,
        string? Variant,
        string[] Palette,
        string Background,
        string CanvasBackgroundColor,
        string WidgetBackgroundColor,
        string WidgetBorderColor,
        string WidgetTitleTextColor,
        string WidgetTextColor,
        string IconColor,
        string Foreground,
        string Accent,
        string Text);

    public record WidgetPositionConfig(int X, int Y, int W, int H);

    public record WidgetColorOverridesConfig(
        string? WidgetBackgroundColor,
        string? WidgetBorderColor,
        string? WidgetTitleTextColor,
        string? WidgetTextColor,
        string? IconColor);

    public record WidgetConfigEntry(
        string Id,
        string Type,
        WidgetPositionConfig Position,
        JsonElement Config,
        WidgetColorOverridesConfig? ColorOverrides,
        string? TitleOverride);

    // Aggregated HA data for rendering
    public class SsrData
    {
        public Dictionary<string, HassEntityState> EntityStates { get; set; } = new();
        public Dictionary<string, List<TodoItem>> TodoItems { get; set; } = new();
        public Dictionary<string, List<CalendarEvent>> CalendarEvents { get; set; } = new();
        public Dictionary<string, List<object?>> WeatherForecasts { get; set; } = new();
        public Dictionary<string, List<RssFeedEntry>> RssFeedEntries { get; set; } = new();
        public Dictionary<string, List<HistoryState>> HistoryData { get; set; } = new();
        public string? SvgIcon { get; set; }
    }

    // =============================================
    // PUBLIC API
    // =============================================

    /// <summary>
    /// Parses the JSON layoutConfig and fetches live HA data, then returns self-contained HTML.
    /// </summary>
    public async Task<string> RenderDashboardHtmlAsync(string dashboardId, string layoutConfigJson)
    {
        var layout = ParseLayout(layoutConfigJson);
        var ssrData = await FetchSsrDataAsync(dashboardId, layout);
        return GenerateHtml(layout, ssrData);
    }

    // =============================================
    // LAYOUT PARSING
    // =============================================

    private LayoutConfig ParseLayout(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var cs = root.GetProperty("colorScheme");
        var paletteArr = cs.GetProperty("palette").EnumerateArray()
            .Select(p => p.GetString() ?? "").ToArray();

        var colorScheme = new ColorSchemeConfig(
            Name: cs.GetProperty("name").GetString() ?? "",
            Variant: cs.TryGetProperty("variant", out var v) ? v.GetString() : null,
            Palette: paletteArr,
            Background: cs.GetProperty("background").GetString() ?? "#ffffff",
            CanvasBackgroundColor: cs.GetProperty("canvasBackgroundColor").GetString() ?? "#ffffff",
            WidgetBackgroundColor: cs.GetProperty("widgetBackgroundColor").GetString() ?? "#ffffff",
            WidgetBorderColor: cs.GetProperty("widgetBorderColor").GetString() ?? "#000000",
            WidgetTitleTextColor: cs.GetProperty("widgetTitleTextColor").GetString() ?? "#000000",
            WidgetTextColor: cs.GetProperty("widgetTextColor").GetString() ?? "#000000",
            IconColor: cs.GetProperty("iconColor").GetString() ?? "#ff0000",
            Foreground: cs.GetProperty("foreground").GetString() ?? "#000000",
            Accent: cs.GetProperty("accent").GetString() ?? "#ff0000",
            Text: cs.GetProperty("text").GetString() ?? "#000000"
        );

        var widgets = new List<WidgetConfigEntry>();
        if (root.TryGetProperty("widgets", out var widgetsArr) && widgetsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in widgetsArr.EnumerateArray())
            {
                var pos = w.GetProperty("position");
                var position = new WidgetPositionConfig(
                    X: pos.GetProperty("x").GetInt32(),
                    Y: pos.GetProperty("y").GetInt32(),
                    W: pos.GetProperty("w").GetInt32(),
                    H: pos.GetProperty("h").GetInt32()
                );

                WidgetColorOverridesConfig? overrides = null;
                if (w.TryGetProperty("colorOverrides", out var co) && co.ValueKind == JsonValueKind.Object)
                {
                    overrides = new WidgetColorOverridesConfig(
                        WidgetBackgroundColor: co.TryGetProperty("widgetBackgroundColor", out var wbg) ? wbg.GetString() : null,
                        WidgetBorderColor: co.TryGetProperty("widgetBorderColor", out var wbc) ? wbc.GetString() : null,
                        WidgetTitleTextColor: co.TryGetProperty("widgetTitleTextColor", out var wttc) ? wttc.GetString() : null,
                        WidgetTextColor: co.TryGetProperty("widgetTextColor", out var wtc) ? wtc.GetString() : null,
                        IconColor: co.TryGetProperty("iconColor", out var ic) ? ic.GetString() : null
                    );
                }

                string? titleOverride = null;
                if (w.TryGetProperty("titleOverride", out var to) && to.ValueKind == JsonValueKind.String)
                {
                    titleOverride = to.GetString();
                }

                widgets.Add(new WidgetConfigEntry(
                    Id: w.GetProperty("id").GetString() ?? "",
                    Type: w.GetProperty("type").GetString() ?? "",
                    Position: position,
                    Config: w.GetProperty("config").Clone(),
                    ColorOverrides: overrides,
                    TitleOverride: titleOverride
                ));
            }
        }

        return new LayoutConfig(
            Width: root.TryGetProperty("width", out var width) ? width.GetInt32() : 800,
            Height: root.TryGetProperty("height", out var height) ? height.GetInt32() : 480,
            GridCols: root.TryGetProperty("gridCols", out var gc) ? gc.GetInt32() : 12,
            GridRows: root.TryGetProperty("gridRows", out var gr) ? gr.GetInt32() : 8,
            ColorScheme: colorScheme,
            Widgets: widgets,
            CanvasPadding: root.TryGetProperty("canvasPadding", out var cp) ? cp.GetInt32() : 16,
            WidgetGap: root.TryGetProperty("widgetGap", out var wg) ? wg.GetInt32() : 4,
            WidgetBorder: root.TryGetProperty("widgetBorder", out var wb) ? wb.GetInt32() : 3,
            TitleFontSize: root.TryGetProperty("titleFontSize", out var tf) ? tf.GetInt32() : 16,
            TextFontSize: root.TryGetProperty("textFontSize", out var txf) ? txf.GetInt32() : 14
        );
    }

    // =============================================
    // DATA FETCHING
    // =============================================

    private async Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout)
    {
        var data = new SsrData();

        // Collect all entity IDs needed across all widgets
        var entityIds = CollectEntityIds(layout);

        // Fetch all entity states in one call
        if (entityIds.Count > 0)
        {
            var statesResult = await _homeAssistantService.FetchEntityStates(dashboardId, entityIds.ToArray());
            if (statesResult.IsSuccess)
            {
                foreach (var state in statesResult.Value)
                    data.EntityStates[state.EntityId] = state;
            }
            else
            {
                _logger.LogWarning("SSR: Failed to fetch entity states: {Error}", statesResult.Error);
            }
        }

        // Fetch todo items per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "todo"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchTodoItems(dashboardId, entityId);
                if (result.IsSuccess) data.TodoItems[entityId] = result.Value;
            }
        }

        // Fetch calendar events per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "calendar"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchCalendarEvents(dashboardId, entityId, 168);
                if (result.IsSuccess) data.CalendarEvents[entityId] = result.Value;
            }
        }

        // Fetch weather forecasts per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "weather-forecast"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
            var forecastType = forecastMode == "hourly" ? "hourly" : "daily";
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchWeatherForecast(dashboardId, entityId, forecastType);
                if (result.IsSuccess
                    && result.Value.TryGetValue("forecast", out var forecastVal)
                    && forecastVal is List<object?> forecastList)
                {
                    data.WeatherForecasts[entityId] = forecastList;
                }
            }
        }

        // Fetch RSS feed entries per widget
        foreach (var widget in layout.Widgets.Where(w => w.Type == "rss-feed"))
        {
            var entityId = GetStringProp(widget.Config, "entityId");
            if (!string.IsNullOrEmpty(entityId))
            {
                var result = await _homeAssistantService.FetchRssFeedEntries(dashboardId, entityId);
                if (result.IsSuccess)
                {
                    data.RssFeedEntries[entityId] = result.Value;
                    _logger.LogDebug("SSR: Fetched {Count} RSS entries for {EntityId}", result.Value.Count, entityId);
                }
                else
                {
                    _logger.LogWarning("SSR: Failed to fetch RSS entries for {EntityId}: {Error}", entityId, result.Error);
                }
            }
        }

        // Fetch entity history for graph widgets
        foreach (var widget in layout.Widgets.Where(w => w.Type == "graph"))
        {
            if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
            {
                var graphEntityIds = series.EnumerateArray()
                    .Select(s => GetStringProp(s, "entityId"))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Cast<string>()
                    .ToList();

                if (graphEntityIds.Count > 0)
                {
                    var periodStr = GetStringProp(widget.Config, "period") ?? "24h";
                    var hours = periodStr switch
                    {
                        "1h" => 1,
                        "6h" => 6,
                        "24h" => 24,
                        "7d" => 168,
                        "30d" => 720,
                        _ => 24
                    };

                    var result = await _homeAssistantService.FetchEntityHistory(dashboardId, graphEntityIds, hours);
                    if (result.IsSuccess)
                    {
                        foreach (var (entityId, states) in result.Value)
                            data.HistoryData[entityId] = states;
                    }
                }
            }
        }

        // Load inline SVG icon
        try
        {
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frontend", "dist", "frontend", "browser");
            var svgPath = Path.Combine(basePath, "icon-tab-dynamic.svg");
            if (!File.Exists(svgPath))
                svgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "icon-tab-dynamic.svg");
            if (File.Exists(svgPath))
                data.SvgIcon = await File.ReadAllTextAsync(svgPath);
        }
        catch { /* ignore */ }

        return data;
    }

    private static HashSet<string> CollectEntityIds(LayoutConfig layout)
    {
        var ids = new HashSet<string>();
        foreach (var widget in layout.Widgets)
        {
            switch (widget.Type)
            {
                case "calendar":
                case "weather":
                case "weather-forecast":
                case "todo":
                case "rss-feed":
                    AddId(widget.Config, "entityId", ids);
                    break;
                case "graph":
                    if (widget.Config.TryGetProperty("series", out var series)
                        && series.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in series.EnumerateArray())
                            AddId(s, "entityId", ids);
                    }
                    break;
                case "header":
                    if (widget.Config.TryGetProperty("badges", out var badges)
                        && badges.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var badge in badges.EnumerateArray())
                            AddId(badge, "entityId", ids);
                    }
                    break;
            }
        }
        return ids;

        static void AddId(JsonElement el, string prop, HashSet<string> ids)
        {
            var val = el.TryGetProperty(prop, out var p) ? p.GetString() : null;
            if (!string.IsNullOrEmpty(val)) ids.Add(val);
        }
    }

    // =============================================
    // HTML PAGE GENERATION
    // =============================================

    private string GenerateHtml(LayoutConfig layout, SsrData data)
    {
        var widgetCss = LoadWidgetCss();

        var sb = new StringBuilder(16384);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Dashboard</title>");
        sb.AppendLine("  <link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@7.1.0/css/all.min.css\" crossorigin=\"anonymous\" />");
        sb.AppendLine("  <style>");
        sb.AppendLine(widgetCss);
        sb.Append(GenerateCanvasCss(layout));
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.Append(GenerateCanvasHtml(layout, data));
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>
    /// Returns only the dashboard-specific dynamic CSS (canvas dimensions, colors, grid).
    /// All static widget styles are loaded from wwwroot/css/ssr-widgets.css.
    /// </summary>
    private static string GenerateCanvasCss(LayoutConfig layout)
    {
        var cs = layout.ColorScheme;

        return $@"
.dashboard-canvas{{
  width:{layout.Width}px;height:{layout.Height}px;
  min-width:{layout.Width}px;min-height:{layout.Height}px;
  background-color:{cs.CanvasBackgroundColor};color:{cs.Text};
  display:grid;
  grid-template-columns:repeat({layout.GridCols},1fr);
  grid-template-rows:repeat({layout.GridRows},1fr);
  gap:{layout.WidgetGap}px;padding:{layout.CanvasPadding}px;
  overflow:hidden;box-sizing:border-box;position:relative;
}}
";
    }

    /// <summary>
    /// Loads the shared widget CSS from wwwroot/css/ssr-widgets.css (cached after first read).
    /// </summary>
    private string LoadWidgetCss()
    {
        if (_cachedWidgetCss != null)
            return _cachedWidgetCss;

        var cssPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "css", "ssr-widgets.css");
        if (File.Exists(cssPath))
        {
            _cachedWidgetCss = File.ReadAllText(cssPath);
            _logger.LogDebug("SSR: Loaded widget CSS from {Path} ({Length} bytes)", cssPath, _cachedWidgetCss.Length);
        }
        else
        {
            _logger.LogWarning("SSR: Widget CSS file not found at {Path}", cssPath);
            _cachedWidgetCss = "/* ssr-widgets.css not found */";
        }

        return _cachedWidgetCss;
    }

    // =============================================
    // CANVAS / WIDGET HTML
    // =============================================

    private string GenerateCanvasHtml(LayoutConfig layout, SsrData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"dashboard-canvas\">");
        foreach (var widget in layout.Widgets)
        {
            var style = GetWidgetContainerStyle(widget, layout);
            sb.AppendLine($"  <div class=\"widget-container\" style=\"{style}\">");
            sb.AppendLine("    <div class=\"widget-preview\">");
            sb.Append(RenderWidget(widget, layout, data));
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
        }
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string GetWidgetContainerStyle(WidgetConfigEntry widget, LayoutConfig layout)
    {
        var cs = layout.ColorScheme;
        var bg = widget.ColorOverrides?.WidgetBackgroundColor ?? cs.WidgetBackgroundColor;
        var bc = widget.ColorOverrides?.WidgetBorderColor ?? cs.WidgetBorderColor;
        var p = widget.Position;
        return $"grid-column:{p.X + 1}/span {p.W};grid-row:{p.Y + 1}/span {p.H};" +
               $"background-color:{bg};border:{layout.WidgetBorder}px solid {bc};color:{cs.Text}";
    }

    private string RenderWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        return widget.Type switch
        {
            "header" => RenderHeaderWidget(widget, layout, data),
            "calendar" => RenderCalendarWidget(widget, layout, data),
            "weather" => RenderWeatherWidget(widget, layout, data),
            "weather-forecast" => RenderWeatherForecastWidget(widget, layout, data),
            "todo" => RenderTodoWidget(widget, layout, data),
            "markdown" => RenderMarkdownWidget(widget, layout),
            "rss-feed" => RenderRssFeedWidget(widget, layout, data),
            "version" => RenderVersionWidget(widget, layout),
            "app-icon" => RenderAppIconWidget(widget, layout, data),
            "image" => RenderImageWidget(widget, layout),
            "graph" => RenderGraphWidget(widget, layout, data),
            _ => $"      <div class=\"preview-state\"><p>{Enc(widget.Type)}</p></div>\n"
        };
    }

    // ==================== HEADER ====================

    private string RenderHeaderWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;

        var title = GetStringProp(widget.Config, "title") ?? "";
        var titleAlign = GetStringProp(widget.Config, "titleAlign") ?? "top-left";
        var iconSize = GetIntProp(widget.Config, "iconSize") ?? 32;
        var isIconOnLeft = titleAlign is "top-left" or "bottom-left";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"header-widget align-{Enc(titleAlign)}\" style=\"color:{titleColor}\">");

        // Title section
        sb.AppendLine("        <div class=\"title-section\">");
        if (isIconOnLeft && data.SvgIcon != null)
        {
            var svg = ApplySvgAccentColor(data.SvgIcon, iconColor);
            sb.AppendLine($"          <div class=\"header-icon\" style=\"width:{iconSize}px;height:{iconSize}px;--accent-color:{iconColor}\">{svg}</div>");
        }
        sb.AppendLine($"          <div class=\"title\" style=\"font-size:{titleFontSize}px;color:{titleColor}\">{Enc(title)}</div>");
        if (!isIconOnLeft && data.SvgIcon != null)
        {
            var svg = ApplySvgAccentColor(data.SvgIcon, iconColor);
            sb.AppendLine($"          <div class=\"header-icon\" style=\"width:{iconSize}px;height:{iconSize}px;--accent-color:{iconColor}\">{svg}</div>");
        }
        sb.AppendLine("        </div>");

        // Badges
        if (widget.Config.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            var hasBadges = badges.EnumerateArray().Any(b =>
                !string.IsNullOrWhiteSpace(b.TryGetProperty("entityId", out var e) ? e.GetString() : null)
                || !string.IsNullOrWhiteSpace(b.TryGetProperty("icon", out var i) ? i.GetString() : null));

            if (hasBadges)
            {
                sb.AppendLine("        <div class=\"badges-container\">");
                foreach (var badge in badges.EnumerateArray())
                {
                    sb.Append($"          <span class=\"badge\" style=\"font-size:{textFontSize}px;color:{textColor}\">");
                    var bIcon = badge.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
                    if (!string.IsNullOrEmpty(bIcon))
                        sb.Append($"<i class=\"fa {Enc(bIcon)}\" style=\"color:{iconColor}\"></i> ");

                    var bEntityId = badge.TryGetProperty("entityId", out var eid) ? eid.GetString() : null;
                    if (!string.IsNullOrEmpty(bEntityId) && data.EntityStates.TryGetValue(bEntityId, out var es))
                    {
                        sb.Append(Enc(es.State));
                        var uom = GetEntityAttr(es, "unit_of_measurement");
                        if (!string.IsNullOrEmpty(uom)) sb.Append($" {Enc(uom)}");
                    }
                    sb.AppendLine("</span>");
                }
                sb.AppendLine("        </div>");
            }
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== CALENDAR ====================

    private string RenderCalendarWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var headerFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var eventFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var maxEvents = GetIntProp(widget.Config, "maxEvents") ?? 7;

        var cssVars = $"--headerFontSize:{headerFontSize}px;--eventFontSize:{eventFontSize}px;" +
                      $"--iconColor:{iconColor};--titleColor:{titleColor};--textColor:{textColor}";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"calendar-widget\" style=\"{cssVars};color:{textColor}\">");

        if (!string.IsNullOrEmpty(entityId)
            && data.CalendarEvents.TryGetValue(entityId, out var events)
            && events.Count > 0)
        {
            sb.AppendLine("        <div class=\"calendar-content\">");
            sb.AppendLine($"          <h4>{Enc(widget.TitleOverride ?? "Events")}</h4>");

            var now = DateTimeOffset.UtcNow;
            var upcoming = events
                .Where(e =>
                {
                    if (DateTimeOffset.TryParse(e.End ?? e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDt))
                        return endDt > now;
                    if (DateTimeOffset.TryParse(e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDt))
                        return startDt >= now;
                    return false;
                })
                .Take(maxEvents).ToList();

            if (upcoming.Count > 0)
            {
                foreach (var ev in upcoming)
                {
                    sb.AppendLine("          <div class=\"calendar-event\">");
                    sb.AppendLine($"            <div class=\"event-datetime\"><i class=\"fa fa-clock\"></i><span>{Enc(FormatEventDate(ev.Start))}</span></div>");
                    sb.AppendLine($"            <div class=\"event-title\">{Enc(ev.Summary ?? ev.Description ?? "-")}</div>");
                    sb.AppendLine("          </div>");
                }
            }
            else
            {
                sb.AppendLine("          <div class=\"empty-state\"><i class=\"fa fa-calendar-days\"></i><p>No upcoming events</p></div>");
            }
            sb.AppendLine("        </div>");
        }
        else
        {
            sb.AppendLine("        <div class=\"preview-state\"><i class=\"fa fa-calendar\"></i><p>Calendar</p></div>");
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== WEATHER ====================

    private string RenderWeatherWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var w = widget.Position.W;
        var h = widget.Position.H;
        var isMinimal = w == 1 && h == 1;
        var isHorizontal = w >= 2 && h < 2;
        var isVerticalCompact = w < 2;
        var isCompact = isVerticalCompact || isHorizontal;

        var cssVars = $"--titleFontSize:{titleFontSize}px;--textFontSize:{textFontSize}px;" +
                      $"--titleColor:{titleColor};--textColor:{textColor};--iconColor:{iconColor}";

        var classes = "weather-widget";
        if (isCompact) classes += " compact";
        if (isHorizontal) classes += " horizontal";
        if (isVerticalCompact) classes += " vertical-compact";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"{classes}\" style=\"{cssVars};color:{textColor}\">");

        if (!string.IsNullOrEmpty(entityId) && data.EntityStates.TryGetValue(entityId, out var es))
        {
            var temperature = GetEntityAttr(es, "temperature");
            if (!string.IsNullOrEmpty(temperature))
            {
                var humidity = GetEntityAttr(es, "humidity");
                var windSpeed = GetEntityAttr(es, "wind_speed");
                var condition = es.State;

                if (isMinimal)
                {
                    sb.AppendLine("        <div class=\"weather-content minimal\">");
                    sb.AppendLine($"          <div class=\"weather-temp\">{Enc(temperature)}°</div>");
                    sb.AppendLine("        </div>");
                }
                else if (isHorizontal)
                {
                    sb.AppendLine("        <div class=\"weather-content horizontal\">");
                    sb.AppendLine($"          <div class=\"weather-condition\">{Enc(condition)}</div>");
                    sb.AppendLine($"          <div class=\"weather-temp\">{Enc(temperature)}°</div>");
                    if (!string.IsNullOrEmpty(humidity) || !string.IsNullOrEmpty(windSpeed))
                    {
                        sb.AppendLine("          <div class=\"weather-attributes-horizontal\">");
                        if (!string.IsNullOrEmpty(humidity))
                            sb.AppendLine($"            <div class=\"weather-attribute-horizontal\" title=\"Humidity\"><i class=\"fa fa-droplet\"></i><span>{Enc(humidity)}%</span></div>");
                        if (!string.IsNullOrEmpty(windSpeed))
                            sb.AppendLine($"            <div class=\"weather-attribute-horizontal\" title=\"Wind Speed\"><i class=\"fa fa-wind\"></i><span>{Enc(windSpeed)}</span></div>");
                        sb.AppendLine("          </div>");
                    }
                    sb.AppendLine("        </div>");
                }
                else
                {
                    sb.AppendLine("        <div class=\"weather-content\">");
                    if (!isVerticalCompact)
                        sb.AppendLine($"          <h4 class=\"weather-title\">{Enc(widget.TitleOverride ?? "Weather")}</h4>");
                    sb.AppendLine($"          <div class=\"weather-condition\">{Enc(condition)}</div>");
                    sb.AppendLine($"          <div class=\"weather-temp\">{Enc(temperature)}°</div>");
                    if (!string.IsNullOrEmpty(humidity) || !string.IsNullOrEmpty(windSpeed))
                    {
                        if (!isVerticalCompact)
                        {
                            sb.AppendLine("          <div class=\"weather-attributes\">");
                            if (!string.IsNullOrEmpty(humidity))
                                sb.AppendLine($"            <div class=\"weather-attribute\"><i class=\"fa fa-droplet\"></i><span>{Enc(humidity)}%</span></div>");
                            if (!string.IsNullOrEmpty(windSpeed))
                                sb.AppendLine($"            <div class=\"weather-attribute\"><i class=\"fa fa-wind\"></i><span>{Enc(windSpeed)}</span></div>");
                            sb.AppendLine("          </div>");
                        }
                        else
                        {
                            sb.AppendLine("          <div class=\"weather-attributes-compact\">");
                            if (!string.IsNullOrEmpty(humidity))
                                sb.AppendLine($"            <div class=\"weather-attribute-compact\" title=\"Humidity\"><i class=\"fa fa-droplet\"></i><span>{Enc(humidity)}%</span></div>");
                            if (!string.IsNullOrEmpty(windSpeed))
                                sb.AppendLine($"            <div class=\"weather-attribute-compact\" title=\"Wind Speed\"><i class=\"fa fa-wind\"></i><span>{Enc(windSpeed)}</span></div>");
                            sb.AppendLine("          </div>");
                        }
                    }
                    sb.AppendLine("        </div>");
                }
            }
            else
            {
                RenderPreviewState(sb, "fa-cloud-sun", "Weather");
            }
        }
        else
        {
            RenderPreviewState(sb, "fa-cloud-sun", "Weather");
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== WEATHER FORECAST ====================

    private string RenderWeatherForecastWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var smallFontSize = (int)Math.Round(textFontSize * 0.75);

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
        var maxItems = GetIntProp(widget.Config, "maxItems");
        var w = widget.Position.W;
        var h = widget.Position.H;
        var isCompact = w <= 2 && h <= 2;
        var isTiny = w <= 2 || h == 1;

        var cssVars = $"--titleFontSize:{titleFontSize}px;--textFontSize:{textFontSize}px;--smallFontSize:{smallFontSize}px;" +
                      $"--titleColor:{titleColor};--textColor:{textColor};--iconColor:{iconColor}";

        var classes = "weather-forecast-widget";
        if (isCompact) classes += " compact";
        if (isTiny) classes += " tiny";
        if (h == 1) classes += " height-1";
        else if (h == 2) classes += " height-2";
        else if (h >= 3) classes += " height-3plus";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"{classes}\" style=\"{cssVars};color:{textColor}\">");

        if (!string.IsNullOrEmpty(entityId)
            && data.WeatherForecasts.TryGetValue(entityId, out var forecastList)
            && forecastList.Count > 0)
        {
            var itemCount = maxItems ?? GetDefaultMaxItems(w, h, forecastMode);
            var items = forecastList.Take(itemCount).ToList();

            // Show header unless tiny
            if (!isTiny)
                sb.AppendLine($"        <div class=\"forecast-header\">{Enc(widget.TitleOverride ?? "Forecast")}</div>");

            // Temperature unit from entity state
            var tempUnit = "°C";
            if (data.EntityStates.TryGetValue(entityId, out var es))
                tempUnit = GetEntityAttr(es, "temperature_unit") ?? "°C";

            var hourlyClass = forecastMode == "hourly" ? " hourly" : "";
            sb.AppendLine($"        <div class=\"forecast-items{hourlyClass}\">");
            foreach (var item in items)
            {
                if (item is not Dictionary<string, object?> dict) continue;

                sb.AppendLine("          <div class=\"forecast-item\">");
                var dt = dict.TryGetValue("datetime", out var dtVal) ? dtVal?.ToString() : "";
                sb.AppendLine($"            <div class=\"item-time\">{Enc(FormatForecastTime(dt, forecastMode))}</div>");

                // Condition (hidden by CSS when height-1 or height-2, but still rendered)
                var cond = dict.TryGetValue("condition", out var condVal) ? FormatCondition(condVal?.ToString()) : "";
                sb.AppendLine($"            <div class=\"item-condition\">{Enc(cond)}</div>");

                if (forecastMode == "hourly")
                {
                    var temp = dict.TryGetValue("temperature", out var tVal) ? RoundNum(tVal) : "";
                    sb.AppendLine($"            <div class=\"item-temp\">{Enc(temp)}{Enc(tempUnit)}</div>");
                }
                else
                {
                    var tempHigh = dict.TryGetValue("temperature", out var thVal) ? RoundNum(thVal) : "";
                    var tempLow = dict.TryGetValue("templow", out var tlVal) ? RoundNum(tlVal) : "";
                    sb.AppendLine($"            <div class=\"item-temps\"><span>{Enc(tempHigh)}{Enc(tempUnit)}</span><span>{Enc(tempLow)}{Enc(tempUnit)}</span></div>");
                }
                sb.AppendLine("          </div>");
            }
            sb.AppendLine("        </div>");
        }
        else
        {
            RenderPreviewState(sb, "fa-cloud-sun-rain", "Forecast");
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== TODO ====================

    private string RenderTodoWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var headerFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var itemFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var smallFontSize = (int)Math.Round(itemFontSize * 0.75);

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var showCompleted = GetBoolProp(widget.Config, "showCompleted") ?? true;
        var w = widget.Position.W;
        var h = widget.Position.H;

        var cssVars = $"--headerFontSize:{headerFontSize}px;--itemFontSize:{itemFontSize}px;--smallFontSize:{smallFontSize}px;" +
                      $"--iconColor:{iconColor};--titleColor:{titleColor};--textColor:{textColor}";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"todo-widget\" style=\"{cssVars};color:{textColor}\">");

        if (!string.IsNullOrEmpty(entityId) && data.TodoItems.TryGetValue(entityId, out var items))
        {
            var mapped = items
                .Select(i => (i.Summary, Complete: i.Status is "completed" or "done"))
                .ToList();

            if (!showCompleted)
                mapped = mapped.Where(i => !i.Complete).ToList();
            mapped = mapped.OrderBy(i => i.Complete ? 1 : 0).ToList();

            sb.AppendLine("        <div class=\"todo-content\">");

            if (w == 1 && h == 1)
            {
                // Compact: show count only
                var pendingCount = mapped.Count(i => !i.Complete);
                sb.AppendLine("          <div class=\"todo-count\">");
                sb.AppendLine("            <i class=\"fa fa-list-check\"></i>");
                sb.AppendLine($"            <span>{pendingCount}</span>");
                sb.AppendLine("            <small>Pending</small>");
                sb.AppendLine("          </div>");
            }
            else
            {
                var friendlyName = "Tasks";
                if (data.EntityStates.TryGetValue(entityId, out var es))
                    friendlyName = GetEntityAttr(es, "friendly_name") ?? "Tasks";

                var displayTitle = widget.TitleOverride ?? friendlyName;
                sb.AppendLine($"          <h4>{Enc(displayTitle)}</h4>");

                var maxShow = Math.Max(1, w * Math.Max(1, h * 2));
                var limited = mapped.Take(maxShow).ToList();

                if (limited.Count > 0)
                {
                    sb.AppendLine("          <div class=\"todo-items\">");
                    foreach (var (summary, complete) in limited)
                    {
                        var iconCls = complete ? "fa-check-circle" : "fa-circle";
                        var spanCls = complete ? " class=\"completed\"" : "";
                        sb.AppendLine("            <div class=\"todo-item\">");
                        sb.AppendLine($"              <i class=\"fa {iconCls}\"></i>");
                        sb.AppendLine($"              <span{spanCls}>{Enc(summary)}</span>");
                        sb.AppendLine("            </div>");
                    }
                    sb.AppendLine("          </div>");
                }
                else
                {
                    sb.AppendLine("          <div class=\"empty-state\"><i class=\"fa fa-list-check\"></i><p>No tasks found</p></div>");
                }
            }

            sb.AppendLine("        </div>");
        }
        else
        {
            RenderPreviewState(sb, "fa-list-check", "Tasks");
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== MARKDOWN ====================

    private static string RenderMarkdownWidget(WidgetConfigEntry widget, LayoutConfig layout)
    {
        var textColor = widget.ColorOverrides?.WidgetTextColor ?? layout.ColorScheme.WidgetTextColor;
        var content = GetStringProp(widget.Config, "content") ?? "";
        var html = ConvertSimpleMarkdown(content);
        return $"      <div class=\"markdown-widget\" style=\"color:{textColor}\"><div class=\"markdown-content\">{html}</div></div>\n";
    }

    // ==================== RSS FEED ====================

    private string RenderRssFeedWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var widgetBg = widget.ColorOverrides?.WidgetBackgroundColor ?? layout.ColorScheme.WidgetBackgroundColor;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var feedTitle = GetStringProp(widget.Config, "title");

        _logger.LogDebug("SSR RSS: entityId={EntityId}, hasEntries={HasEntries}, entryCount={Count}",
            entityId,
            data.RssFeedEntries.ContainsKey(entityId),
            data.RssFeedEntries.TryGetValue(entityId, out var dbgEntries) ? dbgEntries.Count : -1);

        var cssVars = $"--titleFontSize:{titleFontSize}px;--textFontSize:{textFontSize}px;" +
                      $"--iconColor:{iconColor};--titleColor:{titleColor};--textColor:{textColor}";

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"rss-feed-widget\" style=\"{cssVars};color:{textColor}\">");

        if (!string.IsNullOrEmpty(entityId)
            && data.RssFeedEntries.TryGetValue(entityId, out var entries)
            && entries.Count > 0)
        {
            var entry = entries[0]; // Latest entry
            _logger.LogDebug("SSR RSS: Entry title={Title}, link={Link}", entry.Title, entry.Link);

            sb.AppendLine("        <div class=\"rss-feed-content\">");
            var displayTitle = widget.TitleOverride ?? feedTitle;
            if (!string.IsNullOrEmpty(displayTitle))
                sb.AppendLine($"          <h3 class=\"feed-title\">{Enc(displayTitle)}</h3>");
            sb.AppendLine("          <div class=\"rss-entry\">");
            sb.AppendLine("            <div class=\"entry-title-container\">");
            sb.AppendLine($"              <h4 class=\"entry-title\">{Enc(entry.Title)}</h4>");
            sb.AppendLine("            </div>");

            // Generate QR code from entry link
            if (!string.IsNullOrEmpty(entry.Link))
            {
                var qrSvg = GenerateQrCodeSvg(entry.Link, layout.ColorScheme.Text, widgetBg);
                if (!string.IsNullOrEmpty(qrSvg))
                {
                    sb.AppendLine("            <div class=\"qr-code-container\">");
                    sb.AppendLine($"              {qrSvg}");
                    sb.AppendLine("            </div>");
                }
                else
                {
                    _logger.LogWarning("SSR RSS: QR code generation returned empty for link={Link}", entry.Link);
                }
            }
            else
            {
                _logger.LogDebug("SSR RSS: Entry has no link, skipping QR code");
            }

            sb.AppendLine("          </div>");
            sb.AppendLine("        </div>");
        }
        else
        {
            _logger.LogDebug("SSR RSS: No entries found for entityId={EntityId}, showing preview state", entityId);
            RenderPreviewState(sb, "fa-rss", "RSS Feed");
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== VERSION ====================

    private static string RenderVersionWidget(WidgetConfigEntry widget, LayoutConfig layout)
    {
        var textColor = widget.ColorOverrides?.WidgetTextColor ?? layout.ColorScheme.WidgetTextColor;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var version = typeof(DashboardHtmlRenderingService).Assembly.GetName().Version?.ToString() ?? "?";
        return $"      <div class=\"version-widget\" style=\"color:{textColor};font-size:{textFontSize}px\">v{Enc(version)}</div>\n";
    }

    // ==================== APP ICON ====================

    private string RenderAppIconWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var size = GetIntProp(widget.Config, "size") ?? 64;

        var sb = new StringBuilder();
        sb.AppendLine("      <div class=\"app-icon-host\">");
        if (data.SvgIcon != null)
        {
            var svg = ApplySvgAccentColor(data.SvgIcon, iconColor);
            sb.AppendLine($"        <div class=\"app-icon\" style=\"width:{size}px;height:{size}px;max-width:100%;max-height:100%;--accent-color:{iconColor}\">{svg}</div>");
        }
        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // ==================== IMAGE ====================

    private static string RenderImageWidget(WidgetConfigEntry widget, LayoutConfig layout)
    {
        var imageUrl = GetStringProp(widget.Config, "imageUrl") ?? "";
        var fit = GetStringProp(widget.Config, "fit") ?? "contain";
        var titleOverride = widget.TitleOverride;
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var titleColor = widget.ColorOverrides?.WidgetTitleTextColor ?? layout.ColorScheme.WidgetTitleTextColor;
        
        var sb = new StringBuilder();
        sb.AppendLine("      <div class=\"image-widget-wrapper\" style=\"width:100%;height:100%;display:flex;flex-direction:column\">");
        
        if (!string.IsNullOrEmpty(titleOverride))
        {
            sb.AppendLine($"        <h4 class=\"image-title\" style=\"margin:0;padding:8px 12px 4px 12px;font-size:{titleFontSize}px;font-weight:600;color:{titleColor};text-align:center;line-height:1.2;flex-shrink:0\">{Enc(titleOverride)}</h4>");
        }
        
        sb.AppendLine($"        <div class=\"image-widget-container\" style=\"width:100%;flex:1;display:flex;align-items:center;justify-content:center;overflow:hidden;min-height:0\"><img src=\"{Enc(imageUrl)}\" alt=\"Image\" style=\"width:100%;height:100%;object-fit:{fit};object-position:center\" /></div>");
        sb.AppendLine("      </div>");
        
        return sb.ToString();
    }

    // ==================== GRAPH (SVG) ====================

    private string RenderGraphWidget(WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var textColor = ResolveColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleColor = ResolveColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var iconColor = ResolveColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var gridColor = $"{(widget.ColorOverrides?.WidgetBorderColor ?? layout.ColorScheme.WidgetBorderColor)}20";

        var plotType = GetStringProp(widget.Config, "plotType") ?? "line";
        var lineWidth = GetIntProp(widget.Config, "lineWidth") ?? 2;
        var barWidth = GetIntProp(widget.Config, "barWidth") ?? 2;

        // Collect series info
        var seriesList = new List<(string EntityId, string Label, string Color)>();
        if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var s in series.EnumerateArray())
            {
                var sEntityId = GetStringProp(s, "entityId") ?? "";
                var sLabel = GetStringProp(s, "label") ?? sEntityId;
                var sColor = GetStringProp(s, "color") ?? GetDefaultSeriesColor(layout.ColorScheme, idx);
                if (!string.IsNullOrEmpty(sEntityId))
                    seriesList.Add((sEntityId, sLabel, sColor));
                idx++;
            }
        }

        // Check if we have data
        var hasData = seriesList.Any(s => data.HistoryData.ContainsKey(s.EntityId) && data.HistoryData[s.EntityId].Count > 0);

        var sb = new StringBuilder();
        sb.AppendLine($"      <div class=\"graph-widget\" style=\"color:{textColor}\">");

        if (!hasData)
        {
            var cssVars = $"--headerFontSize:{(layout.TitleFontSize > 0 ? layout.TitleFontSize : 15)}px;--iconColor:{iconColor};--titleColor:{titleColor}";
            sb.AppendLine($"        <div class=\"preview-state\" style=\"{cssVars}\"><i class=\"fa fa-chart-line\"></i><p>Graph</p></div>");
        }
        else
        {
            if (!string.IsNullOrEmpty(widget.TitleOverride))
            {
                var headerFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
                sb.AppendLine($"        <h4 class=\"graph-title\" style=\"margin:0;padding:8px 12px 4px 12px;font-size:{headerFontSize}px;font-weight:600;color:{titleColor};text-align:center;line-height:1.2\">{Enc(widget.TitleOverride)}</h4>");
            }
            sb.Append(GenerateSvgChart(seriesList, data.HistoryData, plotType, lineWidth, barWidth,
                textColor, gridColor, textFontSize));
        }

        sb.AppendLine("      </div>");
        return sb.ToString();
    }

    // =============================================
    // SVG CHART GENERATION
    // =============================================

    private static string GenerateSvgChart(
        List<(string EntityId, string Label, string Color)> seriesList,
        Dictionary<string, List<HistoryState>> historyData,
        string plotType, int lineWidth, int barWidth,
        string textColor, string gridColor, int fontSize)
    {
        const int svgW = 400;
        const int svgH = 200;
        // Scale padding based on font size so labels fit regardless of configured size
        var padL = Math.Max(35, fontSize * 4);
        var padR = 10;
        var padT = 10;
        var padB = Math.Max(20, fontSize + 10);
        var plotW = svgW - padL - padR;
        var plotH = svgH - padT - padB;

        // Collect all data points across series
        var allValues = new List<double>();
        var allTimestamps = new List<DateTime>();
        foreach (var (entityId, _, _) in seriesList)
        {
            if (!historyData.TryGetValue(entityId, out var states)) continue;
            foreach (var s in states)
            {
                allValues.Add(s.NumericValue);
                allTimestamps.Add(s.LastChanged);
            }
        }

        if (allValues.Count == 0)
            return "";

        var minVal = allValues.Min();
        var maxVal = allValues.Max();
        if (Math.Abs(maxVal - minVal) < 0.001) { minVal -= 1; maxVal += 1; }
        var valRange = maxVal - minVal;

        var minTime = allTimestamps.Min();
        var maxTime = allTimestamps.Max();
        var timeRange = (maxTime - minTime).TotalSeconds;
        if (timeRange < 1) timeRange = 1;

        var sb = new StringBuilder();
        sb.AppendLine($"        <svg class=\"graph-svg\" viewBox=\"0 0 {svgW} {svgH}\" preserveAspectRatio=\"xMidYMid meet\" xmlns=\"http://www.w3.org/2000/svg\">");

        // Grid lines (4 horizontal)
        for (int i = 0; i <= 3; i++)
        {
            var y = padT + (plotH * i / 3.0);
            sb.AppendLine($"          <line x1=\"{padL}\" y1=\"{y:F1}\" x2=\"{svgW - padR}\" y2=\"{y:F1}\" stroke=\"{gridColor}\" stroke-width=\"0.5\"/>");
            var val = maxVal - (valRange * i / 3.0);
            sb.AppendLine($"          <text x=\"{padL - 4}\" y=\"{y + fontSize * 0.35:F1}\" text-anchor=\"end\" font-size=\"{fontSize}\" fill=\"{textColor}\" opacity=\"0.7\">{val:F0}</text>");
        }

        // X axis line
        sb.AppendLine($"          <line x1=\"{padL}\" y1=\"{padT + plotH}\" x2=\"{svgW - padR}\" y2=\"{padT + plotH}\" stroke=\"{gridColor}\" stroke-width=\"0.5\"/>");

        // X axis labels (5 ticks)
        for (int i = 0; i <= 4; i++)
        {
            var t = minTime.AddSeconds(timeRange * i / 4.0);
            var x = padL + (plotW * i / 4.0);
            var label = t.ToString("HH:mm");
            sb.AppendLine($"          <text x=\"{x:F1}\" y=\"{padT + plotH + fontSize + 4}\" text-anchor=\"middle\" font-size=\"{fontSize}\" fill=\"{textColor}\" opacity=\"0.7\">{label}</text>");
        }

        // Render each series
        if (plotType == "bar")
        {
            var totalSeries = seriesList.Count;
            var seriesIdx = 0;
            foreach (var (entityId, label, color) in seriesList)
            {
                if (!historyData.TryGetValue(entityId, out var states) || states.Count == 0) { seriesIdx++; continue; }

                var bw = Math.Max(2, plotW / (states.Count * totalSeries + 1));
                for (int i = 0; i < states.Count; i++)
                {
                    var s = states[i];
                    var xFrac = (s.LastChanged - minTime).TotalSeconds / timeRange;
                    var x = padL + xFrac * plotW + seriesIdx * bw;
                    var yFrac = (s.NumericValue - minVal) / valRange;
                    var barH = yFrac * plotH;
                    var y = padT + plotH - barH;
                    sb.AppendLine($"          <rect x=\"{x:F1}\" y=\"{y:F1}\" width=\"{bw}\" height=\"{barH:F1}\" fill=\"{color}\" opacity=\"0.8\"/>");
                }
                seriesIdx++;
            }
        }
        else
        {
            // Line chart
            foreach (var (entityId, label, color) in seriesList)
            {
                if (!historyData.TryGetValue(entityId, out var states) || states.Count == 0) continue;

                var points = new StringBuilder();
                foreach (var s in states.OrderBy(s => s.LastChanged))
                {
                    var xFrac = (s.LastChanged - minTime).TotalSeconds / timeRange;
                    var yFrac = (s.NumericValue - minVal) / valRange;
                    var x = padL + xFrac * plotW;
                    var y = padT + plotH - yFrac * plotH;
                    if (points.Length > 0) points.Append(' ');
                    points.Append($"{x:F1},{y:F1}");
                }
                sb.AppendLine($"          <polyline points=\"{points}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{lineWidth}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
            }
        }

        // Legend if multiple series
        if (seriesList.Count > 1)
        {
            var legendY = padT + 2;
            var legendX = padL + 4;
            foreach (var (_, label, color) in seriesList)
            {
                sb.AppendLine($"          <rect x=\"{legendX}\" y=\"{legendY}\" width=\"8\" height=\"8\" fill=\"{color}\" rx=\"1\"/>");
                sb.AppendLine($"          <text x=\"{legendX + 11}\" y=\"{legendY + 7}\" font-size=\"10\" fill=\"{textColor}\">{Enc(label)}</text>");
                legendX += 11 + label.Length * 5 + 8;
            }
        }

        sb.AppendLine("        </svg>");
        return sb.ToString();
    }

    // =============================================
    // QR CODE GENERATION
    // =============================================

    private string GenerateQrCodeSvg(string url, string darkColor, string lightColor)
    {
        try
        {
            _logger.LogDebug("SSR QR: Generating QR code for URL={Url}, dark={Dark}, light={Light}", url, darkColor, lightColor);

            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);
            var svgQrCode = new SvgQRCode(qrCodeData);

            // Use ViewBoxAttribute sizing mode so the SVG scales naturally via CSS
            var svg = svgQrCode.GetGraphic(5, darkColor, lightColor, true,
                SvgQRCode.SizingMode.ViewBoxAttribute);

            if (string.IsNullOrEmpty(svg))
            {
                _logger.LogWarning("SSR QR: SvgQRCode.GetGraphic returned empty");
                return "";
            }

            _logger.LogDebug("SSR QR: Generated SVG length={Length}", svg.Length);

            // Add class for styling
            return svg.Replace("<svg ", "<svg class=\"qr-code-svg\" ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSR QR: Failed to generate QR code for URL={Url}", url);
            return "";
        }
    }

    // =============================================
    // UTILITY HELPERS
    // =============================================

    private static string Enc(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");

    private static string? GetStringProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetIntProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    private static bool? GetBoolProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
            ? p.ValueKind == JsonValueKind.True ? true : p.ValueKind == JsonValueKind.False ? false : null
            : null;

    /// <summary>
    /// Extracts an attribute from HassEntityState.Attributes (Dictionary&lt;string, object?&gt;).
    /// Values may be string, long, double, bool, or nested collections.
    /// </summary>
    private static string? GetEntityAttr(HassEntityState state, string key)
    {
        if (state.Attributes.TryGetValue(key, out var val) && val != null)
        {
            return val switch
            {
                string s => s,
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => val.ToString()
            };
        }
        return null;
    }

    private static string ResolveColor(
        WidgetConfigEntry widget,
        LayoutConfig layout,
        Func<ColorSchemeConfig, string> schemeSelector,
        Func<WidgetColorOverridesConfig?, string?> overrideSelector)
    {
        return overrideSelector(widget.ColorOverrides) ?? schemeSelector(layout.ColorScheme);
    }

    private static string ApplySvgAccentColor(string svg, string accentColor)
    {
        return svg.Replace("--accent-color: #0d6efd;", $"--accent-color: {accentColor};")
                  .Replace("--accent-color:#0d6efd", $"--accent-color:{accentColor}");
    }

    private static void RenderPreviewState(StringBuilder sb, string icon, string label)
    {
        sb.AppendLine($"        <div class=\"preview-state\"><i class=\"fa {icon}\"></i><p>{Enc(label)}</p></div>");
    }

    private static string GetDefaultSeriesColor(ColorSchemeConfig cs, int index)
    {
        var chartColors = cs.Palette
            .Where(c => !string.IsNullOrEmpty(c) && c != cs.Background && c != cs.CanvasBackgroundColor)
            .ToArray();
        if (chartColors.Length > 0)
            return chartColors[index % chartColors.Length];
        var fallback = new[] { "#ff0000", "#00ff00", "#0000ff", "#ffff00", "#ff00ff", "#00ffff" };
        return fallback[index % fallback.Length];
    }

    private static string FormatEventDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            // Date-only (yyyy-MM-dd) → friendly date; datetime → date + time
            return dateStr.Length == 10
                ? dt.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
                : dt.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
        }
        return dateStr;
    }

    private static string FormatForecastTime(string? datetime, string mode)
    {
        if (string.IsNullOrEmpty(datetime)) return "";
        if (!DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return datetime;
        return mode switch
        {
            "hourly" => dt.ToString("HH:mm"),
            "weekly" => dt.ToString("ddd"),
            _ => dt.Day.ToString() // daily
        };
    }

    private static string FormatCondition(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return "";
        return condition.ToLower() switch
        {
            "clear-night" => "Clear",
            "cloudy" => "Cloudy",
            "fog" => "Fog",
            "hail" => "Hail",
            "lightning" => "Storm",
            "lightning-rainy" => "Stormy",
            "partlycloudy" => "Pt. Cloudy",
            "pouring" => "Pouring",
            "rainy" => "Rainy",
            "snowy" => "Snowy",
            "snowy-rainy" => "Snowy Rain",
            "sunny" => "Sunny",
            "windy" => "Windy",
            "windy-variant" => "Windy",
            "exceptional" => "Exceptional",
            _ => condition
        };
    }

    private static string RoundNum(object? val)
    {
        if (val == null) return "";
        if (val is long l) return l.ToString();
        if (val is double d) return Math.Round(d).ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return Math.Round(num).ToString(CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }

    private static int GetDefaultMaxItems(int w, int h, string mode)
    {
        if (w == 1 && h == 1) return 0;
        if (h == 1) return mode switch
        {
            "hourly" => Math.Min(4, w * 2),
            "daily" => Math.Min(2, w),
            "weekly" => 1,
            _ => 2
        };
        if (h == 2) return mode switch
        {
            "hourly" => w switch { 1 => 3, 2 => 5, _ => 7 },
            "daily" => w switch { 1 => 2, 2 => 3, _ => 4 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 3 },
            _ => 3
        };
        return mode switch
        {
            "hourly" => w switch { 1 => 4, 2 => 6, _ => 8 },
            "daily" => w switch { 1 => 2, 2 => 4, _ => 5 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 4 },
            _ => 3
        };
    }

    /// <summary>
    /// Converts a subset of Markdown to HTML (headings, lists, paragraphs).
    /// Covers the most common patterns used in the markdown widget.
    /// </summary>
    private static string ConvertSimpleMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return "";
        var lines = markdown.Split('\n');
        var sb = new StringBuilder();
        var inList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("### ")) { CloseList(ref inList, sb); sb.AppendLine($"<h3>{Enc(line[4..])}</h3>"); }
            else if (line.StartsWith("## ")) { CloseList(ref inList, sb); sb.AppendLine($"<h2>{Enc(line[3..])}</h2>"); }
            else if (line.StartsWith("# ")) { CloseList(ref inList, sb); sb.AppendLine($"<h1>{Enc(line[2..])}</h1>"); }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                if (!inList) { sb.AppendLine("<ul>"); inList = true; }
                sb.AppendLine($"<li>{Enc(line[2..])}</li>");
            }
            else if (string.IsNullOrWhiteSpace(line)) { CloseList(ref inList, sb); }
            else { CloseList(ref inList, sb); sb.AppendLine($"<p>{Enc(line)}</p>"); }
        }
        CloseList(ref inList, sb);
        return sb.ToString();

        static void CloseList(ref bool inList, StringBuilder sb)
        {
            if (inList) { sb.AppendLine("</ul>"); inList = false; }
        }
    }
}
