using System.Text.Json;
using EPaperDashboard.Services;

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
    public Dictionary<string, HassEntityState> EntityStates { get; set; } = new();
    public Dictionary<string, List<TodoItem>> TodoItems { get; set; } = new();
    public Dictionary<string, List<CalendarEvent>> CalendarEvents { get; set; } = new();
    public Dictionary<string, List<object?>> WeatherForecasts { get; set; } = new();
    public Dictionary<string, List<RssFeedEntry>> RssFeedEntries { get; set; } = new();
    public Dictionary<string, List<HistoryState>> HistoryData { get; set; } = new();
    public string? SvgIcon { get; set; }
}
