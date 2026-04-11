using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class AiContentWidgetRenderer(RenderingUtilities utils, MarkdownWidgetRenderer markdownRenderer) : IWidgetRenderer
{
    public string WidgetType => "ai-content";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        if (!data.AiContent.TryGetValue(widget.Id, out var content) || string.IsNullOrWhiteSpace(content))
        {
            RenderPlaceholder(image, widget, layout, contentRect);
            return Task.CompletedTask;
        }

        var syntheticConfig = JsonSerializer.SerializeToElement(new { content });
        var syntheticWidget = widget with { Config = syntheticConfig };
        markdownRenderer.RenderMarkdown(image, syntheticWidget, layout, contentRect);
        return Task.CompletedTask;
    }

    private void RenderPlaceholder(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        TextDrawing.DrawCenteredText(image, "AI Content", utils.GetFont(ctx.TextFontSize), ctx.TextColor, contentRect);
    }
}
