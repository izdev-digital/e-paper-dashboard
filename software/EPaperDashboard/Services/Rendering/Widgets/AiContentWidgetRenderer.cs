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
        contentRect = WidgetFrameRenderer.DrawOptionalCenteredTitle(
            image, widget, layout, utils, contentRect, "AI Content");

        if (!data.AiContent.TryGetValue(widget.Id, out var content) || string.IsNullOrWhiteSpace(content))
        {
            RenderPlaceholder(image, widget, layout, contentRect);
            return Task.CompletedTask;
        }

        var syntheticConfig = JsonSerializer.SerializeToElement(new { content });
        var syntheticWidget = widget with { Config = syntheticConfig, ShowTitle = false };
        markdownRenderer.RenderMarkdown(image, syntheticWidget, layout, contentRect);
        return Task.CompletedTask;
    }

    private void RenderPlaceholder(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var prompt = RenderingUtilities.GetStringProp(widget.Config, "prompt");
        var message = string.IsNullOrWhiteSpace(prompt)
            ? "Configure a prompt to generate AI content"
            : "No generated content cached";
        TextDrawing.DrawCenteredText(image, message, utils.GetFont(ctx.TextFontSize), ctx.TextColor, contentRect);
    }
}
