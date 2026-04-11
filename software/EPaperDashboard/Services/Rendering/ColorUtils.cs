using System.Globalization;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;

namespace EPaperDashboard.Services.Rendering;

public static class ColorUtils
{
    public static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Color.Black;
        try { return Color.ParseHex(hex); }
        catch { return Color.Black; }
    }

    public static Color WithOpacity(Color color, float opacity)
    {
        var p = color.ToPixel<Rgba32>();
        return new Color(new Rgba32(p.R, p.G, p.B, (byte)(p.A * Math.Clamp(opacity, 0f, 1f))));
    }

    public static Color ResolveWidgetColor(
        WidgetConfigEntry widget,
        LayoutConfig layout,
        Func<ColorSchemeConfig, string> schemeSelector,
        Func<WidgetColorOverridesConfig?, string?> overrideSelector)
    {
        var hex = overrideSelector(widget.ColorOverrides) ?? schemeSelector(layout.ColorScheme);
        return ParseColor(hex);
    }

    public static string GetDefaultSeriesColor(ColorSchemeConfig cs, int index)
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
