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
        var ctx = WidgetRenderContext.Create(widget, layout);
        var version = typeof(DashboardImageRenderingService).Assembly.GetName().Version?.ToString() ?? "?";
        utils.DrawCenteredText(image, $"v{version}", utils.GetFont(ctx.TextFontSize, ctx.TextFontWeight), ctx.TextColor, contentRect);
        return Task.CompletedTask;
    }
}
