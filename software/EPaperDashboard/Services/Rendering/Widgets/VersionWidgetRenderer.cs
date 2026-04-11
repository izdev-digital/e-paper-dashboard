using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class VersionWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "version";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var textColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 14;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;
        var version = typeof(DashboardImageRenderingService).Assembly.GetName().Version?.ToString() ?? "?";
        utils.DrawCenteredText(image, $"v{version}", utils.GetFont(textFontSize, textFontWeight), textColor, contentRect);
        return Task.CompletedTask;
    }
}
