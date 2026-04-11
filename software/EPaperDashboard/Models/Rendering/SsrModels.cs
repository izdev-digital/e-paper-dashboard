using System.Collections.Concurrent;
using System.Text.Json;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Rendering;
using Color = SixLabors.ImageSharp.Color;

namespace EPaperDashboard.Models.Rendering;

/// <summary>
/// Dashboard layout configuration parsed from the JSON stored in the database.
/// Mirrors the Angular TS layout types.
/// </summary>
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
    int WidgetPadding,
    int TitleFontSize,
    int TextFontSize,
    int TitleFontWeight,
    int TextFontWeight);

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

public record WidgetPositionConfig(
    int X, int Y, int W, int H,
    double? PixelX = null, double? PixelY = null,
    double? PixelWidth = null, double? PixelHeight = null);

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
    string? TitleOverride = null,
    bool ShowTitle = true);

/// <summary>
/// Aggregated Home Assistant data used for server-side rendering.
/// </summary>
public class SsrData
{
    public ConcurrentDictionary<string, HassEntityState> EntityStates { get; set; } = new();
    public ConcurrentDictionary<string, List<TodoItem>> TodoItems { get; set; } = new();
    public ConcurrentDictionary<string, List<CalendarEvent>> CalendarEvents { get; set; } = new();
    public ConcurrentDictionary<string, List<object?>> WeatherForecasts { get; set; } = new();
    public ConcurrentDictionary<string, List<RssFeedEntry>> RssFeedEntries { get; set; } = new();
    public ConcurrentDictionary<string, List<HistoryState>> HistoryData { get; set; } = new();
    public ConcurrentDictionary<string, string> AiContent { get; set; } = new();
}

/// <summary>
/// Pre-resolved colors and font sizes for a widget, eliminating
/// repeated per-renderer boilerplate.
/// </summary>
public readonly record struct WidgetRenderContext(
    Color TitleColor,
    Color TextColor,
    Color IconColor,
    int TitleFontSize,
    int TextFontSize,
    int TitleFontWeight,
    int TextFontWeight)
{
    public static WidgetRenderContext Create(WidgetConfigEntry widget, LayoutConfig layout) => new(
        TitleColor: ColorUtils.ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor),
        TextColor: ColorUtils.ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor),
        IconColor: ColorUtils.ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor),
        TitleFontSize: layout.TitleFontSize > 0 ? layout.TitleFontSize : 16,
        TextFontSize: layout.TextFontSize > 0 ? layout.TextFontSize : 14,
        TitleFontWeight: layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700,
        TextFontWeight: layout.TextFontWeight > 0 ? layout.TextFontWeight : 400);
}
