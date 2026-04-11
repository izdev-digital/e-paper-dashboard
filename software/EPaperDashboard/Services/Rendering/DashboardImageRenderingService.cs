using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Utilities;
using QRCoder;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;
using Size = EPaperDashboard.Models.Rendering.Size;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Renders a custom dashboard layout directly to an ImageSharp image,
/// without generating HTML or using Playwright. Uses the same parsed
/// layout and fetched HA data as the HTML rendering service.
/// </summary>
public sealed class DashboardImageRenderingService
{
    private readonly ISsrDataProvider _ssrDataProvider;
    private readonly ILogger<DashboardImageRenderingService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly FontFamily _fontFamily;
    private readonly FontAwesomeIconRegistry _iconRegistry;

    public DashboardImageRenderingService(
        ISsrDataProvider ssrDataProvider,
        IWebHostEnvironment env,
        ILogger<DashboardImageRenderingService> logger,
        FontAwesomeIconRegistry iconRegistry)
    {
        _ssrDataProvider = ssrDataProvider;
        _env = env;
        _logger = logger;
        _fontFamily = LoadFontFamily();
        _iconRegistry = iconRegistry;
    }

    /// <summary>
    /// Renders the dashboard to an ImageSharp image using stored configuration and live HA data.
    /// </summary>
    public async Task<Image<Rgba32>> RenderDashboardImageAsync(string dashboardId, string layoutConfigJson)
    {
        var layout = ParseLayout(layoutConfigJson);
        var data = await _ssrDataProvider.FetchSsrDataAsync(dashboardId, layout);
        return RenderToImage(layout, data);
    }

    // =============================================
    // LAYOUT PARSING
    // =============================================

    private LayoutConfig ParseLayout(string json)
    {
        _logger.LogInformation("SSR: Parsing layout JSON (first 1000 chars): {Json}", json.Substring(0, Math.Min(1000, json.Length)));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse color scheme with defaults if missing
        ColorSchemeConfig colorScheme;
        if (root.TryGetProperty("colorScheme", out var cs))
        {
            var paletteArr = cs.TryGetProperty("palette", out var paletteEl) && paletteEl.ValueKind == JsonValueKind.Array
                ? paletteEl.EnumerateArray().Select(p => p.GetString() ?? "").ToArray()
                : new[] { "#000000", "#ffffff", "#ff0000" };

            colorScheme = new ColorSchemeConfig(
                Name: cs.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "") : "Default",
                Variant: cs.TryGetProperty("variant", out var v) ? v.GetString() : null,
                Palette: paletteArr,
                Background: cs.TryGetProperty("background", out var bgEl) ? (bgEl.GetString() ?? "#ffffff") : "#ffffff",
                CanvasBackgroundColor: cs.TryGetProperty("canvasBackgroundColor", out var cbgEl) ? (cbgEl.GetString() ?? "#ffffff") : "#ffffff",
                WidgetBackgroundColor: cs.TryGetProperty("widgetBackgroundColor", out var wbgEl) ? (wbgEl.GetString() ?? "#ffffff") : "#ffffff",
                WidgetBorderColor: cs.TryGetProperty("widgetBorderColor", out var wbcEl) ? (wbcEl.GetString() ?? "#000000") : "#000000",
                WidgetTitleTextColor: cs.TryGetProperty("widgetTitleTextColor", out var wttcEl) ? (wttcEl.GetString() ?? "#000000") : "#000000",
                WidgetTextColor: cs.TryGetProperty("widgetTextColor", out var wtcEl) ? (wtcEl.GetString() ?? "#000000") : "#000000",
                IconColor: cs.TryGetProperty("iconColor", out var icEl) ? (icEl.GetString() ?? "#ff0000") : "#ff0000",
                Foreground: cs.TryGetProperty("foreground", out var fgEl) ? (fgEl.GetString() ?? "#000000") : "#000000",
                Accent: cs.TryGetProperty("accent", out var acEl) ? (acEl.GetString() ?? "#ff0000") : "#ff0000",
                Text: cs.TryGetProperty("text", out var txtEl) ? (txtEl.GetString() ?? "#000000") : "#000000"
            );
        }
        else
        {
            colorScheme = new ColorSchemeConfig(
                Name: "Default",
                Variant: null,
                Palette: new[] { "#000000", "#ffffff", "#ff0000" },
                Background: "#ffffff",
                CanvasBackgroundColor: "#ffffff",
                WidgetBackgroundColor: "#ffffff",
                WidgetBorderColor: "#000000",
                WidgetTitleTextColor: "#000000",
                WidgetTextColor: "#000000",
                IconColor: "#ff0000",
                Foreground: "#000000",
                Accent: "#ff0000",
                Text: "#000000"
            );
        }

        var widgets = new List<WidgetConfigEntry>();
        if (root.TryGetProperty("widgets", out var widgetsArr) && widgetsArr.ValueKind == JsonValueKind.Array)
        {
            _logger.LogInformation("SSR: Found widgets array with {Count} items", widgetsArr.GetArrayLength());
            int widgetIndex = 0;
            foreach (var w in widgetsArr.EnumerateArray())
            {
                widgetIndex++;
                _logger.LogInformation("SSR: Processing widget {Index}: {Widget}", widgetIndex, w.ToString());

                if (!w.TryGetProperty("position", out var pos) ||
                    !w.TryGetProperty("id", out var idEl) ||
                    !w.TryGetProperty("type", out var typeEl) ||
                    !w.TryGetProperty("config", out var configEl))
                {
                    _logger.LogWarning("SSR: Widget {Index} missing required properties - id:{HasId} type:{HasType} position:{HasPos} config:{HasConfig}",
                        widgetIndex,
                        w.TryGetProperty("id", out _),
                        w.TryGetProperty("type", out _),
                        w.TryGetProperty("position", out _),
                        w.TryGetProperty("config", out _));
                    continue;
                }

                if (!pos.TryGetProperty("x", out var xEl) ||
                    !pos.TryGetProperty("y", out var yEl) ||
                    !pos.TryGetProperty("w", out var wEl) ||
                    !pos.TryGetProperty("h", out var hEl))
                {
                    _logger.LogWarning("SSR: Widget {Index} missing position data - x:{HasX} y:{HasY} w:{HasW} h:{HasH}",
                        widgetIndex,
                        pos.TryGetProperty("x", out _),
                        pos.TryGetProperty("y", out _),
                        pos.TryGetProperty("w", out _),
                        pos.TryGetProperty("h", out _));
                    continue;
                }

                var position = new WidgetPositionConfig(
                    X: xEl.GetInt32(),
                    Y: yEl.GetInt32(),
                    W: wEl.GetInt32(),
                    H: hEl.GetInt32(),
                    PixelX: pos.TryGetProperty("pixelX", out var pxEl) && pxEl.ValueKind == JsonValueKind.Number ? pxEl.GetDouble() : null,
                    PixelY: pos.TryGetProperty("pixelY", out var pyEl) && pyEl.ValueKind == JsonValueKind.Number ? pyEl.GetDouble() : null,
                    PixelWidth: pos.TryGetProperty("pixelWidth", out var pwEl) && pwEl.ValueKind == JsonValueKind.Number ? pwEl.GetDouble() : null,
                    PixelHeight: pos.TryGetProperty("pixelHeight", out var phEl) && phEl.ValueKind == JsonValueKind.Number ? phEl.GetDouble() : null
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

                widgets.Add(new WidgetConfigEntry(
                    Id: idEl.GetString() ?? "",
                    Type: typeEl.GetString() ?? "",
                    Position: position,
                    Config: configEl.Clone(),
                    ColorOverrides: overrides,
                    TitleOverride: w.TryGetProperty("titleOverride", out var toEl) ? toEl.GetString() : null,
                    ShowTitle: w.TryGetProperty("showTitle", out var stEl) && stEl.ValueKind == JsonValueKind.False ? false : true
                ));
                _logger.LogInformation("SSR: Successfully parsed widget {Index}: type={Type}, id={Id}, pos=({X},{Y},{W},{H})",
                    widgetIndex, typeEl.GetString(), idEl.GetString(), position.X, position.Y, position.W, position.H);
            }
        }
        else
        {
            _logger.LogWarning("SSR: No widgets property found or not an array in layout JSON");
        }

        _logger.LogInformation("SSR: Parsed {WidgetCount} widgets from layout", widgets.Count);

        return new LayoutConfig(
            Width: root.TryGetProperty("width", out var width) ? width.GetInt32()
                : throw new InvalidOperationException("Layout configuration is missing the 'width' property."),
            Height: root.TryGetProperty("height", out var height) ? height.GetInt32()
                : throw new InvalidOperationException("Layout configuration is missing the 'height' property."),
            GridCols: root.TryGetProperty("gridCols", out var gc) ? gc.GetInt32() : 12,
            GridRows: root.TryGetProperty("gridRows", out var gr) ? gr.GetInt32() : 8,
            ColorScheme: colorScheme,
            Widgets: widgets,
            CanvasPadding: root.TryGetProperty("canvasPadding", out var cp) ? cp.GetInt32() : 16,
            WidgetGap: root.TryGetProperty("widgetGap", out var wg) ? wg.GetInt32() : 4,
            WidgetBorder: root.TryGetProperty("widgetBorder", out var wb) ? wb.GetInt32() : 3,
            WidgetPadding: root.TryGetProperty("widgetPadding", out var wp) ? wp.GetInt32() : 4,
            TitleFontSize: root.TryGetProperty("titleFontSize", out var tf) ? tf.GetInt32() : 16,
            TextFontSize: root.TryGetProperty("textFontSize", out var txf) ? txf.GetInt32() : 14,
            TitleFontWeight: root.TryGetProperty("titleFontWeight", out var tfw) ? tfw.GetInt32() : 700,
            TextFontWeight: root.TryGetProperty("textFontWeight", out var txfw) ? txfw.GetInt32() : 400
        );
    }

    // =============================================
    // FONT LOADING
    // =============================================

    private static FontFamily LoadFontFamily()
    {
        var collection = new FontCollection();

        // Try system fonts first
        if (SystemFonts.TryGet("DejaVu Sans", out var systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Liberation Sans", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Arial", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Helvetica", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Segoe UI", out systemFamily))
            return systemFamily;
        if (SystemFonts.TryGet("Roboto", out systemFamily))
            return systemFamily;

        // Fallback: use any available system font
        foreach (var family in SystemFonts.Families)
            return family;

        throw new InvalidOperationException("No fonts available on the system for rendering.");
    }

    private Font GetFont(int size, FontStyle style = FontStyle.Regular)
    {
        return _fontFamily.CreateFont(size, style);
    }

    private Font GetFont(int size, int weight)
    {
        var style = weight >= 700 ? FontStyle.Bold : FontStyle.Regular;
        return _fontFamily.CreateFont(size, style);
    }

    // =============================================
    // IMAGE RENDERING
    // =============================================

    private Image<Rgba32> RenderToImage(LayoutConfig layout, SsrData data)
    {
        var image = new Image<Rgba32>(layout.Width, layout.Height);

        // Fill canvas background
        var canvasBg = ParseColor(layout.ColorScheme.CanvasBackgroundColor);
        image.Mutate(ctx => ctx.Fill(canvasBg));

        // Render each widget
        foreach (var widget in layout.Widgets)
        {
            RenderWidget(image, widget, layout, data);
        }

        return image;
    }

    private void RenderWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var (px, py, pw, ph) = ResolvePixelPosition(widget.Position, layout);
        var widgetRect = new RectangleF((float)px, (float)py, (float)pw, (float)ph);

        // Draw widget background and border
        DrawWidgetContainer(image, widget, layout, widgetRect);

        // Content area (inside border + padding)
        var border = layout.WidgetBorder;
        var padding = layout.WidgetPadding;
        var inset = border + padding;
        var contentRect = new RectangleF(
            widgetRect.X + inset,
            widgetRect.Y + inset,
            Math.Max(0, widgetRect.Width - inset * 2),
            Math.Max(0, widgetRect.Height - inset * 2));

        if (contentRect.Width <= 0 || contentRect.Height <= 0)
            return;

        try
        {
            switch (widget.Type)
            {
                case "header":
                    RenderHeaderWidget(image, widget, layout, data, contentRect);
                    break;
                case "calendar":
                    RenderCalendarWidget(image, widget, layout, data, contentRect);
                    break;
                case "weather":
                    RenderWeatherWidget(image, widget, layout, data, contentRect);
                    break;
                case "weather-forecast":
                    RenderWeatherForecastWidget(image, widget, layout, data, contentRect);
                    break;
                case "todo":
                    RenderTodoWidget(image, widget, layout, data, contentRect);
                    break;
                case "markdown":
                    RenderMarkdownWidget(image, widget, layout, contentRect);
                    break;
                case "ai-content":
                    RenderAiContentWidget(image, widget, layout, data, contentRect);
                    break;
                case "rss-feed":
                    RenderRssFeedWidget(image, widget, layout, data, contentRect);
                    break;
                case "version":
                    RenderVersionWidget(image, widget, layout, contentRect);
                    break;
                case "app-icon":
                    RenderAppIconWidget(image, widget, layout, data, contentRect);
                    break;
                case "image":
                    RenderImageWidget(image, widget, layout, contentRect);
                    break;
                case "graph":
                    RenderGraphWidget(image, widget, layout, data, contentRect);
                    break;
                default:
                    RenderPlaceholder(image, widget, layout, contentRect, widget.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render widget {WidgetId} of type {WidgetType}", widget.Id, widget.Type);
        }
    }

    // =============================================
    // WIDGET CONTAINER
    // =============================================

    private static void DrawWidgetContainer(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF rect)
    {
        var cs = layout.ColorScheme;
        var bg = ParseColor(widget.ColorOverrides?.WidgetBackgroundColor ?? cs.WidgetBackgroundColor);
        var bc = ParseColor(widget.ColorOverrides?.WidgetBorderColor ?? cs.WidgetBorderColor);
        var borderWidth = layout.WidgetBorder;

        image.Mutate(ctx =>
        {
            // Fill background
            ctx.Fill(bg, new RectangularPolygon(rect));

            // Draw border
            if (borderWidth > 0)
            {
                ctx.Draw(bc, borderWidth, new RectangularPolygon(
                    rect.X + borderWidth / 2f,
                    rect.Y + borderWidth / 2f,
                    rect.Width - borderWidth,
                    rect.Height - borderWidth));
            }
        });
    }

    // =============================================
    // HEADER WIDGET
    // =============================================

    private void RenderHeaderWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var title = GetStringProp(widget.Config, "title") ?? "";
        var iconPosition = GetStringProp(widget.Config, "iconPosition") ?? "left";
        var iconSize = GetIntProp(widget.Config, "iconSize") ?? 32;
        var isIconOnLeft = iconPosition != "right";

        if (widget.ShowTitle && !string.IsNullOrEmpty(title))
        {
            var titleX = GetDoubleProp(widget.Config, "titleX") ?? (isIconOnLeft ? 58.0 : 0.0);
            var titleY = GetDoubleProp(widget.Config, "titleY") ?? 0.0;
            var titleW = GetDoubleProp(widget.Config, "titleW") ?? 42.0;
            var titleH = GetDoubleProp(widget.Config, "titleH") ?? 50.0;

            // The title section region (matches the Angular flex container)
            var sectionRect = new RectangleF(
                contentRect.X + (float)(titleX / 100.0 * contentRect.Width),
                contentRect.Y + (float)(titleY / 100.0 * contentRect.Height),
                (float)(titleW / 100.0 * contentRect.Width),
                (float)(titleH / 100.0 * contentRect.Height));

            // Clamp the icon to the section height, preserving aspect ratio
            var effectiveIconSize = Math.Min(iconSize, sectionRect.Height);

            float textLeftOffset = 0;
            float textRightOffset = 0;

            // Draw app icon — placed inside the title section like the Angular flex layout
            {
                RectangleF iconBounds;
                if (isIconOnLeft)
                {
                    iconBounds = new RectangleF(
                        sectionRect.X,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize,
                        effectiveIconSize);
                    textLeftOffset = effectiveIconSize + 8;
                }
                else
                {
                    iconBounds = new RectangleF(
                        sectionRect.Right - effectiveIconSize,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize,
                        effectiveIconSize);
                    textRightOffset = effectiveIconSize + 8;
                }
                DrawAppIcon(image, iconColor, iconBounds);

                // Apply per-widget dithering to the header icon when configured
                var dithering = GetBoolProp(widget.Config, "dithering") ?? false;
                if (dithering)
                {
                    DitherRegion(image, layout, iconBounds);
                }
            }

            // Title text fills the remaining space beside the icon
            var titleRect = new RectangleF(
                sectionRect.X + textLeftOffset,
                sectionRect.Y,
                sectionRect.Width - textLeftOffset - textRightOffset,
                sectionRect.Height);

            DrawTextEllipsis(image, title, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
        }

        // Render badges
        if (widget.Config.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            int badgeIndex = 0;
            foreach (var badge in badges.EnumerateArray())
            {
                var bEntityId = badge.TryGetProperty("entityId", out var eid) ? eid.GetString() : null;
                var bIcon = badge.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
                bool hasContent = !string.IsNullOrWhiteSpace(bEntityId) || !string.IsNullOrWhiteSpace(bIcon);
                if (!hasContent) { badgeIndex++; continue; }

                var bx = GetBadgeDoubleProp(badge, "x") ?? (badgeIndex % 4) * 22.0;
                var by = GetBadgeDoubleProp(badge, "y") ?? Math.Floor((double)badgeIndex / 4) * 30.0;
                var bw = GetBadgeDoubleProp(badge, "w") ?? 22.0;
                var bh = GetBadgeDoubleProp(badge, "h") ?? 30.0;

                var badgeRect = new RectangleF(
                    contentRect.X + (float)(bx / 100.0 * contentRect.Width),
                    contentRect.Y + (float)(by / 100.0 * contentRect.Height),
                    (float)(bw / 100.0 * contentRect.Width),
                    (float)(bh / 100.0 * contentRect.Height));

                // CSS: .hw-badge { padding: 0 4px; gap: 4px; align-items: center; }
                float badgePadding = 4f;
                float textStartX = badgeRect.X + badgePadding;

                // Draw badge FA icon if present
                if (!string.IsNullOrEmpty(bIcon))
                {
                    var badgeIconSize = textFontSize;
                    var iconBounds = new RectangleF(
                        badgeRect.X + badgePadding,
                        badgeRect.Y + (badgeRect.Height - badgeIconSize) / 2f,
                        badgeIconSize,
                        badgeIconSize);
                    DrawFaIcon(image, bIcon, iconColor, iconBounds);
                    textStartX = iconBounds.Right + 4; // gap: 4px
                }

                if (!string.IsNullOrEmpty(bEntityId) && data.EntityStates.TryGetValue(bEntityId, out var es))
                {
                    var badgeText = es.State;
                    var uom = GetEntityAttr(es, "unit_of_measurement");
                    if (!string.IsNullOrEmpty(uom)) badgeText += $" {uom}";
                    var textRect = new RectangleF(textStartX, badgeRect.Y, badgeRect.Right - textStartX - badgePadding, badgeRect.Height);
                    DrawTextEllipsis(image, badgeText, GetFont(textFontSize, textFontWeight), textColor, textRect);
                }

                badgeIndex++;
            }
        }
    }

    // =============================================
    // CALENDAR WIDGET
    // =============================================

    private void RenderCalendarWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var maxEvents = GetIntProp(widget.Config, "maxEvents") ?? 7;
        var eventGap = GetIntProp(widget.Config, "eventGap") ?? 0;
        var visibleItems = GetCalendarEventItems(widget.Config);

        float yOffset = contentRect.Y;

        if (widget.ShowTitle)
        {
            var titleText = widget.TitleOverride ?? "Events";
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, titleText, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            yOffset += titleFontSize + 6;
        }

        if (!string.IsNullOrEmpty(entityId)
            && data.CalendarEvents.TryGetValue(entityId, out var events)
            && events.Count > 0)
        {
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

            var lineHeight = (int)Math.Ceiling(textFontSize * 1.2f);
            var iconSize = (float)textFontSize;

            foreach (var ev in upcoming)
            {
                if (yOffset + lineHeight > contentRect.Bottom) break;

                foreach (var item in visibleItems)
                {
                    if (yOffset + lineHeight > contentRect.Bottom) break;

                    string? text = item.Type switch
                    {
                        "datetime" => FormatEventDate(ev.Start),
                        "title" => ev.Summary ?? ev.Description ?? "-",
                        "location" => ev.Location,
                        "description" => ev.Description,
                        _ => null
                    };
                    if (string.IsNullOrEmpty(text)) continue;

                    var itemIcon = item.Icon ?? GetDefaultCalendarEventItemIcon(item.Type);
                    float textX = contentRect.X + 4;

                    // Draw icon if present
                    if (!string.IsNullOrEmpty(itemIcon))
                    {
                        var iconBounds = new RectangleF(
                            contentRect.X + 4,
                            yOffset + (lineHeight - iconSize) / 2f,
                            iconSize, iconSize);
                        DrawFaIcon(image, itemIcon, iconColor, iconBounds);
                        textX = iconBounds.Right + 4;
                    }

                    var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, lineHeight);
                    DrawTextEllipsis(image, text, GetFont(textFontSize, textFontWeight), textColor, textRect);
                    yOffset += lineHeight;
                }

                yOffset += eventGap;
            }
        }
    }

    private record CalendarEventItemEntry(string Type, bool Visible, string? Icon, double X, double Y, double W, double H);

    private static List<CalendarEventItemEntry> GetCalendarEventItems(JsonElement config)
    {
        var defaults = new List<CalendarEventItemEntry>
        {
            new("datetime", true, "fa-clock", 0, 0, 100, 50),
            new("title", true, null, 0, 50, 100, 50),
            new("location", false, "fa-location-dot", 0, 50, 100, 25),
            new("description", false, "fa-align-left", 0, 75, 100, 25),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<CalendarEventItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                var def = defaults.FirstOrDefault(d => d.Type == type) ?? defaults[0];
                var x = el.TryGetProperty("x", out var xP) && xP.TryGetDouble(out var xv) ? xv : def.X;
                var y = el.TryGetProperty("y", out var yP) && yP.TryGetDouble(out var yv) ? yv : def.Y;
                var w = el.TryGetProperty("w", out var wP) && wP.TryGetDouble(out var wv) ? wv : def.W;
                var h = el.TryGetProperty("h", out var hP) && hP.TryGetDouble(out var hv) ? hv : def.H;
                if (visible)
                    result.Add(new CalendarEventItemEntry(type, visible, icon, x, y, w, h));
            }
            return result;
        }

        return defaults.Where(d => d.Visible).ToList();
    }

    private static string GetDefaultCalendarEventItemIcon(string type) => type switch
    {
        "datetime" => "fa-clock",
        "title" => "fa-heading",
        "location" => "fa-location-dot",
        "description" => "fa-align-left",
        _ => ""
    };

    // =============================================
    // WEATHER WIDGET
    // =============================================

    private void RenderWeatherWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";

        if (string.IsNullOrEmpty(entityId) || !data.EntityStates.TryGetValue(entityId, out var es))
        {
            return;
        }

        var temperature = GetEntityAttr(es, "temperature") ?? "";
        var condition = es.State ?? "";
        var pressure = GetEntityAttr(es, "pressure") ?? "";

        // Parse items from config, fall back to defaults
        var items = GetWeatherItems(widget.Config);
        var iconSize = (float)textFontSize;

        foreach (var item in items)
        {
            var visible = item.Visible;
            if (item.Type == "title" && !widget.ShowTitle) visible = false;
            if (!visible) continue;

            var itemRect = new RectangleF(
                contentRect.X + (float)(item.X / 100.0 * contentRect.Width),
                contentRect.Y + (float)(item.Y / 100.0 * contentRect.Height),
                (float)(item.W / 100.0 * contentRect.Width),
                (float)(item.H / 100.0 * contentRect.Height));

            switch (item.Type)
            {
                case "title":
                    DrawTextEllipsis(image, widget.TitleOverride ?? "Weather", GetFont(titleFontSize, titleFontWeight), titleColor, itemRect);
                    break;
                case "temperature":
                {
                    var tempIcon = item.Icon ?? "fa-temperature-half";
                    var (textX, textW) = DrawWeatherItemIcon(image, tempIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, $"{temperature}°", GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "condition":
                {
                    var condIcon = item.Icon ?? "fa-cloud-sun";
                    var (textX, textW) = DrawWeatherItemIcon(image, condIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, condition, GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "pressure":
                {
                    var pressIcon = item.Icon ?? "fa-gauge";
                    var (textX, textW) = DrawWeatherItemIcon(image, pressIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, pressure, GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "attribute":
                {
                    var attrKey = item.AttributeKey ?? "humidity";
                    var attrVal = GetEntityAttr(es, attrKey) ?? "";
                    var suffix = attrKey == "humidity" ? "%" : "";
                    var attrIcon = item.Icon ?? attrKey switch
                    {
                        "humidity" => "fa-droplet",
                        "wind_speed" => "fa-wind",
                        _ => "fa-circle-info"
                    };
                    var (textX, textW) = DrawWeatherItemIcon(image, attrIcon, iconColor, iconSize, itemRect);
                    DrawTextEllipsis(image, $"{attrVal}{suffix}", GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Draws a weather item's FA icon on the left side of the item rect and returns the remaining text area.
    /// </summary>
    private (float TextX, float TextW) DrawWeatherItemIcon(Image<Rgba32> image, string? icon, Color iconColor, float iconSize, RectangleF itemRect)
    {
        if (!string.IsNullOrEmpty(icon))
        {
            var iconBounds = new RectangleF(
                itemRect.X + 4,
                itemRect.Y + (itemRect.Height - iconSize) / 2f,
                iconSize, iconSize);
            DrawFaIcon(image, icon, iconColor, iconBounds);
            return (iconBounds.Right + 4, itemRect.Width - iconSize - 8);
        }
        return (itemRect.X, itemRect.Width);
    }

    // =============================================
    // WEATHER FORECAST WIDGET
    // =============================================

    private void RenderWeatherForecastWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var forecastMode = GetStringProp(widget.Config, "forecastMode") ?? "daily";
        var maxItems = GetIntProp(widget.Config, "maxItems");
        var visibleFields = GetStringArrayProp(widget.Config, "visibleFields") ?? new[] { "time", "condition", "tempHigh", "tempLow" };
        if (visibleFields.Contains("temperature"))
            visibleFields = visibleFields.Where(f => f != "temperature").Concat(new[] { "tempHigh", "tempLow" }).Distinct().ToArray();
        var rowGap = GetIntProp(widget.Config, "rowGap") ?? 0;

        float yOffset = contentRect.Y;

        // Title
        if (widget.ShowTitle && widget.Position.H > 1)
        {
            var headerRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, widget.TitleOverride ?? "Forecast", GetFont(titleFontSize, titleFontWeight), titleColor, headerRect);
            yOffset += titleFontSize + 8;
        }

        if (string.IsNullOrEmpty(entityId)
            || !data.WeatherForecasts.TryGetValue(entityId, out var forecastList)
            || forecastList.Count == 0)
        {
            return;
        }

        var w = widget.Position.W;
        var h = widget.Position.H;
        var itemCount = maxItems ?? GetDefaultMaxItems(w, h, forecastMode);
        var items = forecastList.Take(itemCount).ToList();

        // Temperature unit
        var tempUnit = "°C";
        if (data.EntityStates.TryGetValue(entityId, out var es))
            tempUnit = GetEntityAttr(es, "temperature_unit") ?? "°C";

        // Distribute columns evenly
        if (items.Count == 0) return;
        var colGap = 2f;
        var totalGaps = colGap * (items.Count - 1);
        var colWidth = (contentRect.Width - totalGaps) / items.Count;
        var lineHeight = textFontSize + 2;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not Dictionary<string, object?> dict) continue;
            var colX = contentRect.X + i * (colWidth + colGap);
            float itemY = yOffset;

            var dt = dict.TryGetValue("datetime", out var dtVal) ? dtVal?.ToString() : "";
            if (visibleFields.Contains("time"))
            {
                var timeRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, FormatForecastTime(dt, forecastMode), GetFont(textFontSize, textFontWeight), titleColor, timeRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("condition"))
            {
                var condStr = dict.TryGetValue("condition", out var cv) ? FormatCondition(cv?.ToString()) : "";
                var condRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, condStr, GetFont(textFontSize, textFontWeight), textColor, condRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempHigh"))
            {
                var temp = dict.TryGetValue("temperature", out var tVal) ? RoundNum(tVal) : "";
                var tempRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                DrawTextCentered(image, $"{temp}{tempUnit}", GetFont(textFontSize, textFontWeight), textColor, tempRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempLow") && forecastMode != "hourly")
            {
                var tempLow = dict.TryGetValue("templow", out var tlVal) ? RoundNum(tlVal) : "";
                if (!string.IsNullOrEmpty(tempLow))
                {
                    var tlRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{tempLow}{tempUnit}", GetFont(textFontSize, textFontWeight), textColor, tlRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("precipitation"))
            {
                var precip = dict.TryGetValue("precipitation_probability", out var ppVal) ? RoundNum(ppVal) : null;
                if (!string.IsNullOrEmpty(precip))
                {
                    var precipRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{precip}%", GetFont(textFontSize, textFontWeight), textColor, precipRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("wind"))
            {
                var windSpeed = dict.TryGetValue("wind_speed", out var wsVal) ? RoundNum(wsVal) : null;
                if (!string.IsNullOrEmpty(windSpeed))
                {
                    var windUnit = data.EntityStates.TryGetValue(entityId, out var wes) ? GetEntityAttr(wes, "wind_speed_unit") ?? "" : "";
                    var windRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    DrawTextCentered(image, $"{windSpeed} {windUnit}", GetFont(textFontSize, textFontWeight), textColor, windRect);
                }
            }
        }
    }

    // =============================================
    // TODO WIDGET
    // =============================================

    private void RenderTodoWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var showCompleted = GetBoolProp(widget.Config, "showCompleted") ?? true;
        var pendingIcon = GetStringProp(widget.Config, "pendingIcon") ?? "fa-circle";
        var completedIcon = GetStringProp(widget.Config, "completedIcon") ?? "fa-check-circle";
        var w = widget.Position.W;
        var h = widget.Position.H;

        if (string.IsNullOrEmpty(entityId) || !data.TodoItems.TryGetValue(entityId, out var items))
        {
            return;
        }

        var mapped = items
            .Select(i => (i.Summary, Complete: i.Status is "completed" or "done"))
            .ToList();
        if (!showCompleted)
            mapped = mapped.Where(i => !i.Complete).ToList();
        mapped = mapped.OrderBy(i => i.Complete ? 1 : 0).ToList();

        // Compact mode: 1x1 shows count only
        if (w == 1 && h == 1)
        {
            var pendingCount = mapped.Count(i => !i.Complete);
            var listIconSize = Math.Min(contentRect.Width, contentRect.Height) * 0.3f;
            var iconBounds = new RectangleF(
                contentRect.X + (contentRect.Width - listIconSize) / 2f,
                contentRect.Y + contentRect.Height * 0.1f,
                listIconSize, listIconSize);
            DrawFaIcon(image, "fa-list-check", iconColor, iconBounds);

            var countRect = new RectangleF(contentRect.X, iconBounds.Bottom + 2, contentRect.Width, titleFontSize + 4);
            DrawTextCentered(image, pendingCount.ToString(), GetFont(titleFontSize, titleFontWeight), titleColor, countRect);

            var labelRect = new RectangleF(contentRect.X, countRect.Bottom, contentRect.Width, textFontSize + 2);
            DrawTextCentered(image, "Pending", GetFont(textFontSize - 2, textFontWeight), textColor, labelRect);
            return;
        }

        float yOffset = contentRect.Y;

        // Title
        if (widget.ShowTitle)
        {
            var friendlyName = "Tasks";
            if (data.EntityStates.TryGetValue(entityId, out var es))
                friendlyName = GetEntityAttr(es, "friendly_name") ?? "Tasks";
            var titleText = widget.TitleOverride ?? friendlyName;
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, titleText, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            yOffset += titleFontSize + 10;
        }

        var maxShow = GetIntProp(widget.Config, "maxItems") ?? 50;
        var limited = mapped.Take(maxShow).ToList();
        var lineHeight = (int)Math.Ceiling(textFontSize * 1.4f);
        var todoIconSize = (float)textFontSize;
        var todoItemGap = 4;

        foreach (var (summary, complete) in limited)
        {
            if (yOffset + lineHeight > contentRect.Bottom) break;

            // Draw configurable FA icon (flex-start alignment with 2px top margin)
            var itemIconClass = complete ? completedIcon : pendingIcon;
            var iconBounds = new RectangleF(
                contentRect.X,
                yOffset + 2,
                todoIconSize, todoIconSize);
            DrawFaIcon(image, itemIconClass, iconColor, iconBounds);

            // Draw text
            var textX = iconBounds.Right + 6;
            var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, lineHeight);
            DrawTextEllipsis(image, summary, GetFont(textFontSize, textFontWeight), textColor, textRect);
            yOffset += lineHeight + todoItemGap;
        }
    }

    // =============================================
    // MARKDOWN WIDGET
    // =============================================

    private void RenderMarkdownWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;

        var content = GetStringProp(widget.Config, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return;

        var lines = content.Split('\n');
        float yOffset = contentRect.Y;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (yOffset > contentRect.Bottom) break;

            int fontSize;
            int fontWeight;
            string text;
            float xIndent = 0;

            // Headings
            if (line.StartsWith("#### "))
            {
                fontSize = (int)(textFontSize * 1.05);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[5..]);
            }
            else if (line.StartsWith("### "))
            {
                fontSize = (int)(textFontSize * 1.1);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[4..]);
            }
            else if (line.StartsWith("## "))
            {
                fontSize = (int)(titleFontSize * 1.0);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[3..]);
            }
            else if (line.StartsWith("# "))
            {
                fontSize = (int)(titleFontSize * 1.2);
                fontWeight = titleFontWeight;
                text = StripInlineMarkdown(line[2..]);
            }
            // Horizontal rules
            else if (Regex.IsMatch(line, @"^[-*_]{3,}\s*$"))
            {
                var lineY = yOffset + textFontSize / 2f;
                image.Mutate(ctx => ctx.DrawLine(
                    textColor, 1f,
                    new PointF(contentRect.X, lineY),
                    new PointF(contentRect.Right, lineY)));
                yOffset += textFontSize + 2;
                continue;
            }
            // Blockquotes
            else if (line.StartsWith("> ") || line == ">")
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                text = StripInlineMarkdown(line.Length > 2 ? line[2..] : "");
                xIndent = textFontSize * 0.8f;

                // Draw blockquote bar
                var barX = contentRect.X + xIndent * 0.3f;
                var barTop = yOffset;
                var barBottom = yOffset + fontSize + 4;
                image.Mutate(ctx => ctx.DrawLine(
                    textColor, 2f,
                    new PointF(barX, barTop),
                    new PointF(barX, barBottom)));
            }
            // Unordered lists
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                text = $"• {StripInlineMarkdown(line[2..])}";
            }
            // Numbered lists (e.g. "1. item", "12. item")
            else if (Regex.IsMatch(line, @"^\d+\.\s"))
            {
                fontSize = textFontSize;
                fontWeight = textFontWeight;
                var match = Regex.Match(line, @"^(\d+\.)\s(.*)$");
                text = match.Success
                    ? $"{match.Groups[1].Value} {StripInlineMarkdown(match.Groups[2].Value)}"
                    : StripInlineMarkdown(line);
            }
            // Empty lines
            else if (string.IsNullOrWhiteSpace(line))
            {
                yOffset += textFontSize / 2f;
                continue;
            }
            // Regular paragraph text - strip inline markdown formatting
            else
            {
                fontSize = textFontSize;
                // Use bold weight if line is entirely bold
                fontWeight = IsEntirelyBold(line) ? titleFontWeight : textFontWeight;
                text = StripInlineMarkdown(line);
            }

            var lineHeight = fontSize + 4;
            var lineRect = new RectangleF(contentRect.X + xIndent, yOffset, contentRect.Width - xIndent, lineHeight);
            DrawTextEllipsis(image, text, GetFont(fontSize, fontWeight), textColor, lineRect);
            yOffset += lineHeight;
        }
    }

    // =============================================
    // AI CONTENT WIDGET (renders content as markdown)
    // =============================================

    private void RenderAiContentWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        // AI content is generated at render time and stored in SsrData.AiContent by widget ID.
        if (!data.AiContent.TryGetValue(widget.Id, out var content) || string.IsNullOrWhiteSpace(content))
        {
            RenderPlaceholder(image, widget, layout, contentRect, "AI Content");
            return;
        }

        // Reuse the markdown rendering logic by swapping in the content
        var syntheticConfig = System.Text.Json.JsonSerializer.SerializeToElement(new { content });
        var syntheticWidget = widget with { Config = syntheticConfig };
        RenderMarkdownWidget(image, syntheticWidget, layout, contentRect);
    }

    /// <summary>
    /// Strips inline markdown formatting syntax, preserving the visible text content.
    /// Handles: bold, italic, strikethrough, inline code, links, and images.
    /// </summary>
    private static string StripInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Images: ![alt](url) → alt
        text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]*\)", "$1");
        // Links: [text](url) → text
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        // Bold+italic: ***text*** or ___text___
        text = Regex.Replace(text, @"\*{3}(.+?)\*{3}", "$1");
        text = Regex.Replace(text, @"_{3}(.+?)_{3}", "$1");
        // Bold: **text** or __text__
        text = Regex.Replace(text, @"\*{2}(.+?)\*{2}", "$1");
        text = Regex.Replace(text, @"_{2}(.+?)_{2}", "$1");
        // Italic: *text* or _text_
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"(?<=\s|^)_(.+?)_(?=\s|$)", "$1");
        // Strikethrough: ~~text~~
        text = Regex.Replace(text, @"~~(.+?)~~", "$1");
        // Inline code: `code`
        text = Regex.Replace(text, @"`(.+?)`", "$1");

        return text;
    }

    /// <summary>
    /// Checks if a line consists entirely of bold text (e.g. "**some text**").
    /// </summary>
    private static bool IsEntirelyBold(string line)
    {
        var trimmed = line.Trim();
        return (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
            || (trimmed.StartsWith("__") && trimmed.EndsWith("__") && trimmed.Length > 4);
    }

    // =============================================
    // RSS FEED WIDGET
    // =============================================

    private void RenderRssFeedWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 16;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var widgetBg = widget.ColorOverrides?.WidgetBackgroundColor ?? layout.ColorScheme.WidgetBackgroundColor;

        var entityId = GetStringProp(widget.Config, "entityId") ?? "";
        var feedTitle = GetStringProp(widget.Config, "title");

        if (string.IsNullOrEmpty(entityId)
            || !data.RssFeedEntries.TryGetValue(entityId, out var entries)
            || entries.Count == 0)
        {
            return;
        }

        var entry = entries[0];
        float yOffset = contentRect.Y;

        // Feed title
        if (widget.ShowTitle && !string.IsNullOrEmpty(widget.TitleOverride ?? feedTitle))
        {
            var feedTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            DrawTextEllipsis(image, widget.TitleOverride ?? feedTitle!, GetFont(titleFontSize, titleFontWeight), titleColor, feedTitleRect);
            yOffset += titleFontSize + 8;
        }

        // Entry title (word-wrapped, max 3 lines to match frontend behavior)
        var entryTitleFont = GetFont(textFontSize, textFontWeight);
        var entryLineHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(entryTitleFont)).Height;
        var maxEntryLines = Math.Max(1, (int)((contentRect.Bottom - yOffset - 8) / entryLineHeight));
        maxEntryLines = Math.Min(maxEntryLines, 2);
        var entryTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, entryLineHeight * maxEntryLines);
        var entryTitleHeight = DrawWrappedTextEllipsis(image, entry.Title, entryTitleFont, titleColor, entryTitleRect, maxEntryLines);
        yOffset += entryTitleHeight + 8;

        // QR code (rendered as ImageSharp image from QRCoder)
        if (!string.IsNullOrEmpty(entry.Link))
        {
            try
            {
                var qrSize = Math.Min(contentRect.Width, contentRect.Bottom - yOffset);
                if (qrSize > 20)
                {
                    var darkColor = ParseColor(layout.ColorScheme.Text);
                    var lightColor = ParseColor(widgetBg);
                    var qrImage = GenerateQrCodeImage(entry.Link, darkColor, lightColor, (int)qrSize);
                    if (qrImage != null)
                    {
                        var qrX = (int)(contentRect.X + (contentRect.Width - qrSize) / 2);
                        var qrY = (int)yOffset;
                        image.Mutate(ctx => ctx.DrawImage(qrImage, new SixLabors.ImageSharp.Point(qrX, qrY), 1f));
                        qrImage.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render QR code for RSS entry");
            }
        }
    }

    // =============================================
    // VERSION WIDGET
    // =============================================

    private void RenderVersionWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var version = typeof(DashboardImageRenderingService).Assembly.GetName().Version?.ToString() ?? "?";
        DrawCenteredText(image, $"v{version}", GetFont(textFontSize, textFontWeight), textColor, contentRect);
    }

    // =============================================
    // APP-ICON WIDGET
    // =============================================

    private void RenderAppIconWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var iconColor = ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var size = GetIntProp(widget.Config, "size") ?? 64;

        // Center the icon in the content rect, capped to configured size
        var actualSize = Math.Min(size, Math.Min(contentRect.Width, contentRect.Height));
        var iconBounds = new RectangleF(
            contentRect.X + (contentRect.Width - actualSize) / 2f,
            contentRect.Y + (contentRect.Height - actualSize) / 2f,
            actualSize, actualSize);
        DrawAppIcon(image, iconColor, iconBounds);

        // Apply per-widget dithering when configured
        var dithering = GetBoolProp(widget.Config, "dithering") ?? false;
        if (dithering)
        {
            DitherRegion(image, layout, iconBounds);
        }
    }

    // =============================================
    // IMAGE WIDGET
    // =============================================

    private void RenderImageWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var imageUrl = GetStringProp(widget.Config, "imageUrl") ?? "";
        if (string.IsNullOrEmpty(imageUrl)) return;

        try
        {
            byte[] imageBytes;

            // Images are stored on disk and served via /api/dashboards/{id}/images/{file}
            // Load directly from disk instead of making an HTTP request to ourselves.
            var localMatch = System.Text.RegularExpressions.Regex.Match(
                imageUrl, @"^/api/dashboards/([^/]+)/images/([^/]+)$");
            if (localMatch.Success)
            {
                var dashId = localMatch.Groups[1].Value;
                var fileName = localMatch.Groups[2].Value;
                // Guard against traversal
                if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
                    return;
                var filePath = System.IO.Path.Combine(
                    Utilities.EnvironmentConfiguration.ConfigDir, "uploads", dashId, fileName);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Image file not found on disk: {Path}", filePath);
                    return;
                }
                imageBytes = File.ReadAllBytes(filePath);
            }
            else
            {
                // Fallback: external URL
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                imageBytes = httpClient.GetByteArrayAsync(imageUrl).GetAwaiter().GetResult();
            }

            using var srcImage = Image.Load<Rgba32>(imageBytes);

            var zoom = GetDoubleProp(widget.Config, "zoom") ?? 1.0;
            var panX = GetDoubleProp(widget.Config, "offsetX") ?? 0.0;
            var panY = GetDoubleProp(widget.Config, "offsetY") ?? 0.0;

            var containerW = contentRect.Width;
            var containerH = contentRect.Height;

            // The Angular component sets the img element to (zoom * 100%) of the container,
            // then uses object-fit: contain to preserve aspect ratio while keeping the
            // entire image within the element so panning can reach all edges.
            var imgElW = containerW * (float)zoom;
            var imgElH = containerH * (float)zoom;

            // Fit the source image within the virtual img element (object-fit: contain)
            float srcAspect = (float)srcImage.Width / srcImage.Height;
            float elAspect = imgElW / imgElH;

            float drawW, drawH;
            if (srcAspect > elAspect)
            {
                // Source is wider → constrained by width
                drawW = imgElW;
                drawH = imgElW / srcAspect;
            }
            else
            {
                // Source is taller → constrained by height
                drawH = imgElH;
                drawW = imgElH * srcAspect;
            }

            // Center the fitted image within the virtual element
            float fitOffsetX = (imgElW - drawW) / 2f;
            float fitOffsetY = (imgElH - drawH) / 2f;

            // Angular positions the img element at:
            //   left = -((zoom - 1) * (offsetX + 1) * 50)%   of container width
            //   top  = -((zoom - 1) * (offsetY + 1) * 50)%   of container height
            float elLeft = -(float)((zoom - 1) * (panX + 1) * 50.0 / 100.0) * containerW;
            float elTop = -(float)((zoom - 1) * (panY + 1) * 50.0 / 100.0) * containerH;

            // Final draw position = container origin + element offset + fit centering
            float drawX = contentRect.X + elLeft + fitOffsetX;
            float drawY = contentRect.Y + elTop + fitOffsetY;

            // Resize source image to the fitted dimensions
            var resizedW = Math.Max(1, (int)Math.Round(drawW));
            var resizedH = Math.Max(1, (int)Math.Round(drawH));
            srcImage.Mutate(ctx => ctx.Resize(new SixLabors.ImageSharp.Size(resizedW, resizedH)));

            // Apply per-widget dithering to the source image before compositing
            var dithering = GetBoolProp(widget.Config, "dithering") ?? false;
            {
                var paletteColors = layout.ColorScheme.Palette
                    .Select(hex => ParseColor(hex))
                    .ToArray();
                if (paletteColors.Length > 0)
                {
                    srcImage.Mutate(ctx => ctx.Quantize(new PaletteQuantizer(
                        new ReadOnlyMemory<Color>(paletteColors),
                        new QuantizerOptions { Dither = dithering ? KnownDitherings.JarvisJudiceNinke : null })));
                }
            }

            // Clip the source image to the visible portion within the content rect
            // (the Angular container has overflow: hidden)
            int srcDrawX = (int)Math.Round(drawX);
            int srcDrawY = (int)Math.Round(drawY);

            int clipLeft = Math.Max(0, (int)contentRect.X - srcDrawX);
            int clipTop = Math.Max(0, (int)contentRect.Y - srcDrawY);
            int clipRight = Math.Min(srcImage.Width, (int)(contentRect.X + contentRect.Width) - srcDrawX);
            int clipBottom = Math.Min(srcImage.Height, (int)(contentRect.Y + contentRect.Height) - srcDrawY);

            int clipW = clipRight - clipLeft;
            int clipH = clipBottom - clipTop;

            if (clipW > 0 && clipH > 0)
            {
                using var clipped = srcImage.Clone(ctx =>
                    ctx.Crop(new SixLabors.ImageSharp.Rectangle(clipLeft, clipTop, clipW, clipH)));

                image.Mutate(ctx => ctx.DrawImage(clipped,
                    new SixLabors.ImageSharp.Point(srcDrawX + clipLeft, srcDrawY + clipTop), 1f));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load image from URL: {Url}", imageUrl);
            var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
            DrawCenteredText(image, "Image", GetFont(layout.TextFontSize > 0 ? layout.TextFontSize : 12), textColor, contentRect);
        }
    }

    // =============================================
    // GRAPH WIDGET
    // =============================================

    private void RenderGraphWidget(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleColor = ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var gridColorStr = (widget.ColorOverrides?.WidgetBorderColor ?? layout.ColorScheme.WidgetBorderColor);

        // Render title if configured (matches frontend graph-title)
        if (widget.ShowTitle && !string.IsNullOrEmpty(widget.TitleOverride))
        {
            var titleRect = new RectangleF(contentRect.X, contentRect.Y, contentRect.Width, titleFontSize + 8);
            DrawTextCentered(image, widget.TitleOverride, GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            contentRect = new RectangleF(contentRect.X, contentRect.Y + titleFontSize + 8, contentRect.Width, contentRect.Height - titleFontSize - 8);
        }

        var plotType = GetStringProp(widget.Config, "plotType") ?? "line";
        var lineWidth = GetIntProp(widget.Config, "lineWidth") ?? 2;
        var barWidth = GetIntProp(widget.Config, "barWidth") ?? 2;

        var seriesList = new List<(string EntityId, string Label, string Color)>();
        if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
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

        var hasData = seriesList.Any(s => data.HistoryData.ContainsKey(s.EntityId) && data.HistoryData[s.EntityId].Count > 0);
        if (!hasData)
        {
            DrawCenteredText(image, "Graph", GetFont(textFontSize), titleColor, contentRect);
            return;
        }

        // Collect all data points
        var allValues = new List<double>();
        var allTimestamps = new List<DateTime>();
        foreach (var (entityId, _, _) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states)) continue;
            foreach (var s in states)
            {
                allValues.Add(s.NumericValue);
                allTimestamps.Add(s.LastChanged);
            }
        }

        if (allValues.Count == 0) return;

        var minVal = allValues.Min();
        var maxVal = allValues.Max();
        if (Math.Abs(maxVal - minVal) < 0.001) { minVal -= 1; maxVal += 1; }
        var valRange = maxVal - minVal;

        var minTime = allTimestamps.Min();
        var maxTime = allTimestamps.Max();
        var timeRange = (maxTime - minTime).TotalSeconds;
        if (timeRange < 1) timeRange = 1;

        var padL = Math.Max(35, textFontSize * 4);
        var padR = 10f;
        var padT = 10f;
        var padB = Math.Max(20, textFontSize + 10);
        var plotW = contentRect.Width - padL - padR;
        var plotH = contentRect.Height - padT - padB;
        var originX = contentRect.X + padL;
        var originY = contentRect.Y + padT;

        var gridColor = ParseColor(gridColorStr + "33");
        var labelFont = GetFont(Math.Max(8, textFontSize - 2));

        // Grid lines
        image.Mutate(ctx =>
        {
            for (int i = 0; i <= 3; i++)
            {
                var y = originY + plotH * i / 3f;
                ctx.DrawLine(gridColor, 0.5f, new PointF(originX, y), new PointF(originX + plotW, y));

                var val = maxVal - (valRange * i / 3.0);
                var labelRect = new RectangleF(contentRect.X, y - textFontSize / 2f, padL - 4, textFontSize);
                DrawTextAligned(ctx, image, $"{val:F0}", labelFont, textColor, labelRect, HorizontalAlignment.Right);
            }

            // X axis
            ctx.DrawLine(gridColor, 0.5f,
                new PointF(originX, originY + plotH),
                new PointF(originX + plotW, originY + plotH));
        });

        // X axis labels
        for (int i = 0; i <= 4; i++)
        {
            var t = minTime.AddSeconds(timeRange * i / 4.0);
            var x = originX + plotW * i / 4f;
            var labelRect = new RectangleF(x - 20, originY + plotH + 4, 40, textFontSize + 4);
            DrawTextCentered(image, t.ToString("HH:mm"), labelFont, textColor, labelRect);
        }

        // Render series
        foreach (var (entityId, label, color) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states) || states.Count == 0) continue;
            var seriesColor = ParseColor(color);
            var ordered = states.OrderBy(s => s.LastChanged).ToList();

            if (plotType == "bar")
            {
                var bw = Math.Max(2, plotW / (ordered.Count + 1));
                image.Mutate(ctx =>
                {
                    foreach (var s in ordered)
                    {
                        var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                        var x = originX + xFrac * plotW;
                        var yFrac = (float)((s.NumericValue - minVal) / valRange);
                        var barH = yFrac * plotH;
                        var y = originY + plotH - barH;
                        ctx.Fill(seriesColor, new RectangularPolygon(x, y, bw, barH));
                    }
                });
            }
            else
            {
                // Line chart
                if (ordered.Count < 2) continue;
                var points = ordered.Select(s =>
                {
                    var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                    var yFrac = (float)((s.NumericValue - minVal) / valRange);
                    return new PointF(originX + xFrac * plotW, originY + plotH - yFrac * plotH);
                }).ToArray();

                image.Mutate(ctx => ctx.DrawLine(seriesColor, lineWidth, points));
            }
        }
    }

    // =============================================
    // PLACEHOLDER (for unsupported widget types)
    // =============================================

    private void RenderPlaceholder(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect, string label)
    {
        var textColor = ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var fontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        DrawCenteredText(image, label, GetFont(fontSize), textColor, contentRect);
    }

    // =============================================
    // TEXT DRAWING HELPERS
    // =============================================

    /// <summary>
    /// Draws text within a bounding rectangle, truncating with ellipsis if it would overflow.
    /// </summary>
    private void DrawTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var measuredSize = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // If text fits, draw it directly
        if (measuredSize.Width <= bounds.Width)
        {
            var y = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(bounds.X, y)));
            return;
        }

        // Truncate with ellipsis
        var ellipsis = "…";
        var ellipsisSize = TextMeasurer.MeasureSize(ellipsis, new TextOptions(font));
        var availableWidth = bounds.Width - ellipsisSize.Width;

        if (availableWidth <= 0)
        {
            // Not even room for ellipsis — draw what fits
            var cy = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(
                new RichTextOptions(font)
                {
                    Origin = new PointF(bounds.X, cy),
                    WrappingLength = bounds.Width,
                    WordBreaking = WordBreaking.BreakAll,
                },
                text, new SolidBrush(color), null));
            return;
        }

        // Binary search for the truncation point
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            var subSize = TextMeasurer.MeasureSize(text[..mid], new TextOptions(font));
            if (subSize.Width <= availableWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        var truncated = lo > 0 ? text[..lo] + ellipsis : ellipsis;
        var truncSize = TextMeasurer.MeasureSize(truncated, new TextOptions(font));
        var ty = bounds.Y + (bounds.Height - truncSize.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(truncated, font, color, new PointF(bounds.X, ty)));
    }

    /// <summary>
    /// Draws text with word-wrapping within a bounding rectangle, up to a maximum number of lines.
    /// The last visible line is truncated with ellipsis if the text doesn't fully fit.
    /// Returns the total height consumed.
    /// </summary>
    private float DrawWrappedTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, int maxLines = int.MaxValue)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0 || maxLines <= 0)
            return 0;

        var lineHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(font)).Height;
        var ellipsis = "…";
        var ellipsisWidth = TextMeasurer.MeasureSize(ellipsis, new TextOptions(font)).Width;

        // Split text into words
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 0;

        var lines = new List<string>();
        var currentLine = words[0];

        for (int i = 1; i < words.Length; i++)
        {
            var candidate = currentLine + " " + words[i];
            var candidateWidth = TextMeasurer.MeasureSize(candidate, new TextOptions(font)).Width;

            if (candidateWidth <= bounds.Width)
            {
                currentLine = candidate;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = words[i];

                // If we already have enough lines, break early
                if (lines.Count >= maxLines) break;
            }
        }

        if (lines.Count < maxLines)
        {
            lines.Add(currentLine);
        }

        var needsEllipsis = lines.Count > maxLines ||
            (lines.Count == maxLines && words.Length > 0 && !text.EndsWith(lines[^1]));

        // Trim to maxLines
        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
            needsEllipsis = true;
        }

        // Check if the original text was fully consumed
        var reconstructed = string.Join(" ", lines);
        if (reconstructed.Length < text.Replace("  ", " ").Trim().Length)
        {
            needsEllipsis = true;
        }

        float yOffset = bounds.Y;
        for (int i = 0; i < lines.Count; i++)
        {
            if (yOffset + lineHeight > bounds.Bottom + 1) break;

            var line = lines[i];
            var isLastLine = i == lines.Count - 1;

            if (isLastLine && needsEllipsis)
            {
                // Truncate last line with ellipsis if it overflows
                var lineWidth = TextMeasurer.MeasureSize(line, new TextOptions(font)).Width;
                var availableWidth = bounds.Width - ellipsisWidth;

                if (lineWidth > availableWidth && availableWidth > 0)
                {
                    // Binary search for truncation point
                    int lo = 0, hi = line.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        var subWidth = TextMeasurer.MeasureSize(line[..mid], new TextOptions(font)).Width;
                        if (subWidth <= availableWidth)
                            lo = mid;
                        else
                            hi = mid - 1;
                    }
                    line = (lo > 0 ? line[..lo].TrimEnd() : "") + ellipsis;
                }
                else
                {
                    line = line.TrimEnd() + ellipsis;
                }
            }
            else if (isLastLine)
            {
                // Even on the last line, if a single word is too wide, truncate it
                var lineWidth = TextMeasurer.MeasureSize(line, new TextOptions(font)).Width;
                if (lineWidth > bounds.Width)
                {
                    var availableWidth = bounds.Width - ellipsisWidth;
                    int lo = 0, hi = line.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        var subWidth = TextMeasurer.MeasureSize(line[..mid], new TextOptions(font)).Width;
                        if (subWidth <= availableWidth)
                            lo = mid;
                        else
                            hi = mid - 1;
                    }
                    line = (lo > 0 ? line[..lo] : "") + ellipsis;
                }
            }

            image.Mutate(ctx => ctx.DrawText(line, font, color, new PointF(bounds.X, yOffset)));
            yOffset += lineHeight;
        }

        return yOffset - bounds.Y;
    }

    /// <summary>
    /// Draws text centered within a bounding rectangle.
    /// </summary>
    private void DrawCenteredText(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    /// <summary>
    /// Draws text centered horizontally within a bounding rectangle, vertically centered.
    /// </summary>
    private void DrawTextCentered(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));

        // Truncate if too wide
        if (size.Width > bounds.Width)
        {
            DrawTextEllipsis(image, text, font, color, bounds);
            return;
        }

        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    /// <summary>
    /// Draws text with horizontal alignment within a bounding rectangle.
    /// </summary>
    private static void DrawTextAligned(IImageProcessingContext ctx, Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, HorizontalAlignment alignment)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = alignment switch
        {
            HorizontalAlignment.Right => bounds.Right - size.Width,
            HorizontalAlignment.Center => bounds.X + (bounds.Width - size.Width) / 2f,
            _ => bounds.X
        };
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        ctx.DrawText(text, font, color, new PointF(x, y));
    }

    private enum HorizontalAlignment { Left, Center, Right }

    // =============================================
    // FA ICON DRAWING
    // =============================================

    /// <summary>
    /// Draws a Font Awesome icon (from the icon registry) scaled to fit the given bounds.
    /// </summary>
    private void DrawFaIcon(Image<Rgba32> image, string? iconClass, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(iconClass) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (!_iconRegistry.TryGetIcon(iconClass, out var entry))
            return;

        try
        {
            var path = SvgPathParser.Parse(entry.Path);
            var pathBounds = path.Bounds;
            if (pathBounds.Width < 0.1f || pathBounds.Height < 0.1f)
                return;

            // Use viewBox dimensions for scaling
            var vbW = entry.VbW;
            var vbH = entry.VbH;

            var scale = Math.Min(bounds.Width / vbW, bounds.Height / vbH);
            var offsetX = bounds.X + (bounds.Width - vbW * scale) / 2f;
            var offsetY = bounds.Y + (bounds.Height - vbH * scale) / 2f;

            var matrix = Matrix3x2.CreateScale(scale) *
                         Matrix3x2.CreateTranslation(offsetX, offsetY);

            var transformed = path.Transform(matrix);
            image.Mutate(ctx => ctx.Fill(color, transformed));
        }
        catch
        {
            // Silently ignore icon rendering failures
        }
    }

    /// <summary>
    /// Draws the app dashboard icon directly using ImageSharp primitives.
    /// Reproduces the layout from icon-tab-dynamic.svg (viewBox 0 0 370 370):
    /// a 2-column grid of rounded rectangles and two diagonal polygons.
    /// </summary>
    private static void DrawAppIcon(Image<Rgba32> image, Color accentColor, RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        const float vb = 370f;
        var scale = Math.Min(bounds.Width / vb, bounds.Height / vb);
        var ox = bounds.X + (bounds.Width - vb * scale) / 2f;
        var oy = bounds.Y + (bounds.Height - vb * scale) / 2f;

        // Accent color shades (matching the SVG CSS classes)
        var p = accentColor.ToPixel<Rgba32>();
        var darkest  = new Color(new Rgba32((byte)(p.R * 0.3f), (byte)(p.G * 0.3f), (byte)(p.B * 0.3f), p.A));
        var darker   = new Color(new Rgba32((byte)(p.R * 0.7f), (byte)(p.G * 0.7f), (byte)(p.B * 0.7f), p.A));
        var baseC    = accentColor;
        var light    = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.2f), (byte)(p.G + (255 - p.G) * 0.2f), (byte)(p.B + (255 - p.B) * 0.2f), p.A));
        var lighter  = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.4f), (byte)(p.G + (255 - p.G) * 0.4f), (byte)(p.B + (255 - p.B) * 0.4f), p.A));
        var lightest = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.6f), (byte)(p.G + (255 - p.G) * 0.6f), (byte)(p.B + (255 - p.B) * 0.6f), p.A));

        // Shape definitions: (x, y, w, h, rx, color)
        (float x, float y, float w, float h, float rx, Color c)[] rects =
        [
            (20, 20, 90, 96, 4, darkest),   // top-left small
            (20, 128, 90, 196, 4, darker),   // left tall
            (122, 20, 134, 96, 4, baseC),    // top-center wide
            (268, 20, 82, 96, 4, light),     // top-right
            (122, 236, 84, 88, 4, light),    // bottom-left
            (218, 236, 132, 88, 4, lighter), // bottom-right
        ];

        foreach (var (rx, ry, rw, rh, rrx, color) in rects)
        {
            var x1 = rx * scale + ox;
            var y1 = ry * scale + oy;
            var w1 = rw * scale;
            var h1 = rh * scale;
            var cr = Math.Min(rrx * scale, Math.Min(w1, h1) / 2f);
            image.Mutate(ctx => ctx.Fill(color, BuildRoundedRect(x1, y1, w1, h1, cr)));
        }

        // Middle row diagonal split — two trapezoid polygons
        // At icon scale the rounded-corner clip is sub-pixel, so draw directly.
        // Left trapezoid (lightest)
        PointF[] leftPoly = [
            new(122 * scale + ox, 128 * scale + oy),
            new(256 * scale + ox, 128 * scale + oy),
            new(206 * scale + ox, 224 * scale + oy),
            new(122 * scale + ox, 224 * scale + oy),
        ];
        // Right trapezoid (lighter)
        PointF[] rightPoly = [
            new(268 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 224 * scale + oy),
            new(218 * scale + ox, 224 * scale + oy),
        ];

        image.Mutate(ctx =>
        {
            ctx.Fill(lightest, new Polygon(new LinearLineSegment(leftPoly)));
            ctx.Fill(lighter, new Polygon(new LinearLineSegment(rightPoly)));
        });
    }

    /// <summary>
    /// Builds a rounded rectangle IPath from position, size, and corner radius.
    /// </summary>
    private static IPath BuildRoundedRect(float x, float y, float w, float h, float cr)
    {
        if (cr < 0.5f)
            return new RectangularPolygon(x, y, w, h);

        cr = Math.Min(cr, Math.Min(w, h) / 2f);
        var pb = new PathBuilder();
        pb.MoveTo(new PointF(x + cr, y));
        pb.LineTo(new PointF(x + w - cr, y));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w, y + cr));
        pb.LineTo(new PointF(x + w, y + h - cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w - cr, y + h));
        pb.LineTo(new PointF(x + cr, y + h));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x, y + h - cr));
        pb.LineTo(new PointF(x, y + cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + cr, y));
        pb.CloseFigure();
        return pb.Build();
    }

    /// <summary>
    /// Applies palette-based dithering to a rectangular sub-region of the target image.
    /// Extracts the region, quantises it using the layout colour palette and
    /// Jarvis-Judice-Ninke error-diffusion, then draws the result back.
    /// </summary>
    private static void DitherRegion(Image<Rgba32> image, LayoutConfig layout, RectangleF region)
    {
        // Clamp to image bounds
        int rx = Math.Max(0, (int)region.X);
        int ry = Math.Max(0, (int)region.Y);
        int rw = Math.Min(image.Width - rx, Math.Max(1, (int)Math.Ceiling(region.Width)));
        int rh = Math.Min(image.Height - ry, Math.Max(1, (int)Math.Ceiling(region.Height)));
        if (rw <= 0 || rh <= 0) return;

        // Build palette colours from the scheme
        var paletteColors = layout.ColorScheme.Palette
            .Select(hex => ParseColor(hex))
            .ToArray();
        if (paletteColors.Length == 0) return;

        // Extract the sub-region into a temporary image
        using var sub = image.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(rx, ry, rw, rh)));

        // Quantise with dithering
        sub.Mutate(ctx => ctx.Quantize(new PaletteQuantizer(
            new ReadOnlyMemory<Color>(paletteColors),
            new QuantizerOptions { Dither = KnownDitherings.JarvisJudiceNinke })));

        // Draw the quantised sub-image back onto the main image
        image.Mutate(ctx => ctx.DrawImage(sub, new SixLabors.ImageSharp.Point(rx, ry), 1f));
    }

    // =============================================
    // QR CODE GENERATION
    // =============================================

    private Image<Rgba32>? GenerateQrCodeImage(string url, Color darkColor, Color lightColor, int size)
    {
        try
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);

            // Use PNG QR code and load into ImageSharp
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var darkRgba = darkColor.ToPixel<Rgba32>();
            var lightRgba = lightColor.ToPixel<Rgba32>();
            var pngBytes = pngQrCode.GetGraphic(
                20,
                new byte[] { darkRgba.R, darkRgba.G, darkRgba.B, darkRgba.A },
                new byte[] { lightRgba.R, lightRgba.G, lightRgba.B, lightRgba.A });

            var qrImage = Image.Load<Rgba32>(pngBytes);
            qrImage.Mutate(ctx => ctx.Resize(new SixLabors.ImageSharp.Size(size, size)));
            return qrImage;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate QR code for URL: {Url}", url);
            return null;
        }
    }

    // =============================================
    // PIXEL POSITION RESOLUTION (matches HTML service)
    // =============================================

    private static (double X, double Y, double Width, double Height) ResolvePixelPosition(WidgetPositionConfig pos, LayoutConfig layout)
    {
        if (pos.PixelX.HasValue && pos.PixelY.HasValue && pos.PixelWidth.HasValue && pos.PixelHeight.HasValue)
        {
            return (pos.PixelX.Value, pos.PixelY.Value, pos.PixelWidth.Value, pos.PixelHeight.Value);
        }

        var padding = layout.CanvasPadding;
        var gap = layout.WidgetGap;
        var cols = Math.Max(1, layout.GridCols);
        var rows = Math.Max(1, layout.GridRows);
        var innerWidth = Math.Max(0, layout.Width - padding * 2 - gap * (cols - 1));
        var innerHeight = Math.Max(0, layout.Height - padding * 2 - gap * (rows - 1));
        var cellWidth = (double)innerWidth / cols;
        var cellHeight = (double)innerHeight / rows;

        var x = padding + pos.X * (cellWidth + gap);
        var y = padding + pos.Y * (cellHeight + gap);
        var w = pos.W * cellWidth + (pos.W - 1) * gap;
        var h = pos.H * cellHeight + (pos.H - 1) * gap;

        return (Math.Round(x * 100) / 100, Math.Round(y * 100) / 100, Math.Round(w * 100) / 100, Math.Round(h * 100) / 100);
    }

    // =============================================
    // COLOR HELPERS
    // =============================================

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Color.Black;

        try
        {
            return Color.ParseHex(hex);
        }
        catch
        {
            return Color.Black;
        }
    }

    private static Color ResolveWidgetColor(
        WidgetConfigEntry widget,
        LayoutConfig layout,
        Func<ColorSchemeConfig, string> schemeSelector,
        Func<WidgetColorOverridesConfig?, string?> overrideSelector)
    {
        var hex = overrideSelector(widget.ColorOverrides) ?? schemeSelector(layout.ColorScheme);
        return ParseColor(hex);
    }

    // =============================================
    // JSON PROPERTY HELPERS (mirrors HTML service)
    // =============================================

    private static string? GetStringProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static int? GetIntProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    private static double? GetDoubleProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    private static bool? GetBoolProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
            ? p.ValueKind == JsonValueKind.True ? true : p.ValueKind == JsonValueKind.False ? false : null
            : null;

    private static string[]? GetStringArrayProp(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Array)
            return null;
        return p.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .ToArray();
    }

    private static double? GetBadgeDoubleProp(JsonElement badge, string prop) =>
        badge.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

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

    // =============================================
    // WEATHER ITEMS PARSING (mirrors HTML service)
    // =============================================

    private record WeatherItemEntry(string Type, bool Visible, double X, double Y, double W, double H, string? AttributeKey, string? Label, string? Icon);

    private List<WeatherItemEntry> GetWeatherItems(JsonElement config)
    {
        var defaults = new List<WeatherItemEntry>
        {
            new("title", true, 0, 0, 100, 20, null, null, null),
            new("temperature", true, 0, 22, 50, 20, null, null, "fa-temperature-half"),
            new("condition", true, 50, 22, 50, 20, null, null, "fa-cloud-sun"),
            new("pressure", true, 0, 44, 50, 20, null, null, "fa-gauge"),
            new("attribute", true, 50, 44, 50, 20, "humidity", "Humidity", "fa-droplet"),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<WeatherItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var x = el.TryGetProperty("x", out var xProp) ? xProp.GetDouble() : 0;
                var y = el.TryGetProperty("y", out var yProp) ? yProp.GetDouble() : 0;
                var w = el.TryGetProperty("w", out var wProp) ? wProp.GetDouble() : 100;
                var h = el.TryGetProperty("h", out var hProp) ? hProp.GetDouble() : 20;
                var attrKey = el.TryGetProperty("attributeKey", out var akProp) ? akProp.GetString() : null;
                var label = el.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                result.Add(new WeatherItemEntry(type, visible, x, y, w, h, attrKey, label, icon));
            }
            return result;
        }

        return defaults;
    }

    // =============================================
    // FORMAT HELPERS (mirrors HTML service)
    // =============================================

    private static string FormatEventDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
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
            _ => dt.Day.ToString()
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
}
