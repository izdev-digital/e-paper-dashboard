using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Shared frame layout for widgets whose content sits below an optional centered title.
/// Keeping title measurement here prevents individual renderers from drifting apart.
/// </summary>
public static class WidgetFrameRenderer
{
    public static RectangleF DrawOptionalCenteredTitle(
        Image<Rgba32> image,
        WidgetConfigEntry widget,
        LayoutConfig layout,
        RenderingUtilities utils,
        RectangleF contentRect,
        string? fallbackTitle = null)
    {
        var title = widget.TitleOverride ?? fallbackTitle;
        if (!widget.ShowTitle || string.IsNullOrWhiteSpace(title))
            return contentRect;

        var context = WidgetRenderContext.Create(widget, layout);
        var titleHeight = Math.Min(contentRect.Height, context.TitleFontSize + 8);
        var titleRect = new RectangleF(
            contentRect.X + 12,
            contentRect.Y,
            Math.Max(0, contentRect.Width - 24),
            titleHeight);

        TextDrawing.DrawTextCentered(
            image,
            title,
            utils.GetFont(context.TitleFontSize, context.TitleFontWeight),
            context.TitleColor,
            titleRect);

        return new RectangleF(
            contentRect.X,
            contentRect.Y + titleHeight,
            contentRect.Width,
            Math.Max(0, contentRect.Height - titleHeight));
    }
}
