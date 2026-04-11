using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Providers;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Renders a custom dashboard layout directly to an ImageSharp image,
/// without generating HTML or using Playwright. Coordinates widget renderers
/// and provides the overall rendering pipeline.
/// </summary>
public sealed class DashboardImageRenderingService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly ISsrDataProvider _ssrDataProvider;
    private readonly ILogger<DashboardImageRenderingService> _logger;
    private readonly RenderingUtilities _utils;
    private readonly Dictionary<string, IWidgetRenderer> _renderers;
    private readonly IMemoryCache _cache;

    public DashboardImageRenderingService(
        ISsrDataProvider ssrDataProvider,
        ILogger<DashboardImageRenderingService> logger,
        RenderingUtilities utils,
        IEnumerable<IWidgetRenderer> widgetRenderers,
        IMemoryCache cache)
    {
        _ssrDataProvider = ssrDataProvider;
        _logger = logger;
        _utils = utils;
        _renderers = widgetRenderers.ToDictionary(r => r.WidgetType, r => r);
        _cache = cache;
    }

    /// <summary>
    /// Returns the shared <see cref="RenderingUtilities"/> instance for DI consumers
    /// that need to create widget renderers with font/icon support.
    /// </summary>
    public RenderingUtilities Utils => _utils;

    /// <summary>
    /// Renders the dashboard to an ImageSharp image using the typed layout model and live HA data.
    /// Returns a cached result if the same dashboard was rendered within the last 30 seconds.
    /// </summary>
    public async Task<Image<Rgba32>> RenderDashboardImageAsync(string dashboardId, Models.LayoutConfig layoutConfig)
    {
        var cacheKey = $"ssr:{dashboardId}";

        if (_cache.TryGetValue<byte[]>(cacheKey, out var cachedBytes) && cachedBytes is not null)
        {
            _logger.LogDebug("SSR: Returning cached render for dashboard {DashboardId}", dashboardId);
            return Image.Load<Rgba32>(cachedBytes);
        }

        var layout = ConvertLayout(layoutConfig);
        var data = await _ssrDataProvider.FetchSsrDataAsync(dashboardId, layout);
        var image = await RenderToImageAsync(layout, data);

        // Cache the rendered image as PNG bytes
        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        _cache.Set(cacheKey, ms.ToArray(), CacheDuration);

        return image;
    }

    // =============================================
    // LAYOUT CONVERSION (typed model → rendering record)
    // =============================================

    private static LayoutConfig ConvertLayout(Models.LayoutConfig src)
    {
        var cs = src.ColorScheme;
        var colorScheme = new ColorSchemeConfig(
            Name: cs.Name ?? "Default",
            Variant: cs.Variant,
            Palette: cs.Palette?.ToArray() ?? ["#000000", "#ffffff", "#ff0000"],
            Background: DefaultIfEmpty(cs.Background, "#ffffff"),
            CanvasBackgroundColor: DefaultIfEmpty(cs.CanvasBackgroundColor, "#ffffff"),
            WidgetBackgroundColor: DefaultIfEmpty(cs.WidgetBackgroundColor, "#ffffff"),
            WidgetBorderColor: DefaultIfEmpty(cs.WidgetBorderColor, "#000000"),
            WidgetTitleTextColor: DefaultIfEmpty(cs.WidgetTitleTextColor, "#000000"),
            WidgetTextColor: DefaultIfEmpty(cs.WidgetTextColor, "#000000"),
            IconColor: DefaultIfEmpty(cs.IconColor, "#ff0000"),
            Foreground: DefaultIfEmpty(cs.Foreground, "#000000"),
            Accent: DefaultIfEmpty(cs.Accent, "#ff0000"),
            Text: DefaultIfEmpty(cs.Text, "#000000")
        );

        var widgets = src.Widgets
            .Where(w => !string.IsNullOrEmpty(w.Id) && !string.IsNullOrEmpty(w.Type))
            .Select(w => new WidgetConfigEntry(
                Id: w.Id,
                Type: w.Type,
                Position: new WidgetPositionConfig(
                    w.Position.X, w.Position.Y, w.Position.W, w.Position.H,
                    w.Position.PixelX, w.Position.PixelY,
                    w.Position.PixelWidth, w.Position.PixelHeight),
                Config: w.Config.ValueKind != JsonValueKind.Undefined ? w.Config.Clone() : default,
                ColorOverrides: w.ColorOverrides is { } co
                    ? new WidgetColorOverridesConfig(
                        co.WidgetBackgroundColor, co.WidgetBorderColor,
                        co.WidgetTitleTextColor, co.WidgetTextColor, co.IconColor)
                    : null,
                TitleOverride: w.TitleOverride,
                ShowTitle: true))
            .ToList();

        return new LayoutConfig(
            Width: src.Width,
            Height: src.Height,
            GridCols: src.GridCols > 0 ? src.GridCols : 12,
            GridRows: src.GridRows > 0 ? src.GridRows : 8,
            ColorScheme: colorScheme,
            Widgets: widgets,
            CanvasPadding: src.CanvasPadding,
            WidgetGap: src.WidgetGap,
            WidgetBorder: src.WidgetBorder,
            WidgetPadding: src.WidgetPadding,
            TitleFontSize: src.TitleFontSize > 0 ? src.TitleFontSize : 16,
            TextFontSize: src.TextFontSize > 0 ? src.TextFontSize : 14,
            TitleFontWeight: src.TitleFontWeight > 0 ? src.TitleFontWeight : 700,
            TextFontWeight: src.TextFontWeight > 0 ? src.TextFontWeight : 400
        );
    }

    private static string DefaultIfEmpty(string? value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;

    // =============================================
    // IMAGE RENDERING
    // =============================================

    private async Task<Image<Rgba32>> RenderToImageAsync(LayoutConfig layout, SsrData data)
    {
        var image = new Image<Rgba32>(layout.Width, layout.Height);

        var canvasBg = RenderingUtilities.ParseColor(layout.ColorScheme.CanvasBackgroundColor);
        image.Mutate(ctx => ctx.Fill(canvasBg));

        foreach (var widget in layout.Widgets)
        {
            await RenderWidgetAsync(image, widget, layout, data);
        }

        return image;
    }

    private async Task RenderWidgetAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data)
    {
        var (px, py, pw, ph) = RenderingUtilities.ResolvePixelPosition(widget.Position, layout);
        var widgetRect = new RectangleF((float)px, (float)py, (float)pw, (float)ph);

        DrawWidgetContainer(image, widget, layout, widgetRect);

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
            if (_renderers.TryGetValue(widget.Type, out var renderer))
            {
                await renderer.RenderAsync(image, widget, layout, data, contentRect);
            }
            else
            {
                RenderPlaceholder(image, widget, layout, contentRect, widget.Type);
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
        var bg = RenderingUtilities.ParseColor(widget.ColorOverrides?.WidgetBackgroundColor ?? cs.WidgetBackgroundColor);
        var bc = RenderingUtilities.ParseColor(widget.ColorOverrides?.WidgetBorderColor ?? cs.WidgetBorderColor);
        var borderWidth = layout.WidgetBorder;

        image.Mutate(ctx =>
        {
            if (borderWidth > 0)
            {
                ctx.Fill(bc, new RectangularPolygon(rect));
                ctx.Fill(bg, new RectangularPolygon(
                    rect.X + borderWidth,
                    rect.Y + borderWidth,
                    Math.Max(0, rect.Width - borderWidth * 2),
                    Math.Max(0, rect.Height - borderWidth * 2)));
            }
            else
            {
                ctx.Fill(bg, new RectangularPolygon(rect));
            }
        });
    }

    // =============================================
    // PLACEHOLDER (for unsupported widget types)
    // =============================================

    private void RenderPlaceholder(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect, string label)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        _utils.DrawCenteredText(image, label, _utils.GetFont(ctx.TextFontSize), ctx.TextColor, contentRect);
    }
}
