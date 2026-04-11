using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class AppIconWidgetRenderer : IWidgetRenderer
{
    public string WidgetType => "app-icon";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var size = RenderingUtilities.GetIntProp(widget.Config, "size") ?? 64;

        var actualSize = Math.Min(size, Math.Min(contentRect.Width, contentRect.Height));
        var iconBounds = new RectangleF(
            contentRect.X + (contentRect.Width - actualSize) / 2f,
            contentRect.Y + (contentRect.Height - actualSize) / 2f,
            actualSize, actualSize);
        RenderingUtilities.DrawAppIcon(image, ctx.IconColor, iconBounds);

        var dithering = RenderingUtilities.GetBoolProp(widget.Config, "dithering") ?? false;
        if (dithering)
            RenderingUtilities.DitherRegion(image, layout, iconBounds);

        return Task.CompletedTask;
    }
}
