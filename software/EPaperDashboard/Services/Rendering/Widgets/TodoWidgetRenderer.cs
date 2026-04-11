using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class TodoWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "todo";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var iconColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.IconColor, o => o?.IconColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";
        var showCompleted = RenderingUtilities.GetBoolProp(widget.Config, "showCompleted") ?? true;
        var pendingIcon = RenderingUtilities.GetStringProp(widget.Config, "pendingIcon") ?? "fa-circle";
        var completedIcon = RenderingUtilities.GetStringProp(widget.Config, "completedIcon") ?? "fa-check-circle";
        var w = widget.Position.W;
        var h = widget.Position.H;

        if (string.IsNullOrEmpty(entityId) || !data.TodoItems.TryGetValue(entityId, out var items))
            return Task.CompletedTask;

        var mapped = items
            .Select(i => (i.Summary, Complete: i.Status is "completed" or "done"))
            .ToList();
        if (!showCompleted)
            mapped = mapped.Where(i => !i.Complete).ToList();
        mapped = mapped.OrderBy(i => i.Complete ? 1 : 0).ToList();

        // Compact mode: 1x1
        if (w == 1 && h == 1)
        {
            var pendingCount = mapped.Count(i => !i.Complete);
            var compactIconSize = textFontSize * 1.5f;
            var countFontSize = (int)Math.Round(textFontSize * 1.5);
            var labelFontSize = (int)Math.Round(textFontSize * 0.75);
            var iconBounds = new RectangleF(
                contentRect.X + (contentRect.Width - compactIconSize) / 2f,
                contentRect.Y + contentRect.Height * 0.1f,
                compactIconSize, compactIconSize);
            utils.DrawFaIcon(image, "fa-list-check", iconColor, iconBounds);

            var countRect = new RectangleF(contentRect.X, iconBounds.Bottom + 2, contentRect.Width, countFontSize + 4);
            utils.DrawTextCentered(image, pendingCount.ToString(), utils.GetFont(countFontSize, textFontWeight), titleColor, countRect);

            var labelRect = new RectangleF(contentRect.X, countRect.Bottom, contentRect.Width, labelFontSize + 2);
            utils.DrawTextCentered(image, "Pending", utils.GetFont(labelFontSize, textFontWeight), textColor, labelRect);
            return Task.CompletedTask;
        }

        float yOffset = contentRect.Y;

        if (widget.ShowTitle)
        {
            var friendlyName = "Tasks";
            if (data.EntityStates.TryGetValue(entityId, out var es))
                friendlyName = RenderingUtilities.GetEntityAttr(es, "friendly_name") ?? "Tasks";
            var titleText = widget.TitleOverride ?? friendlyName;
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            utils.DrawTextEllipsis(image, titleText, utils.GetFont(titleFontSize, titleFontWeight), RenderingUtilities.WithOpacity(titleColor, 0.9f), titleRect);
            yOffset += titleFontSize + 10;
        }

        var maxShow = RenderingUtilities.GetIntProp(widget.Config, "maxItems") ?? 50;
        var limited = mapped.Take(maxShow).ToList();
        var lineHeight = (int)Math.Ceiling(textFontSize * 1.4f);
        var todoIconSize = (float)textFontSize;
        var todoItemGap = 4;
        var todoFont = utils.GetFont(textFontSize, textFontWeight);
        var todoGlyphHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(todoFont)).Height;
        var todoExtraSpacing = Math.Max(0, textFontSize * 1.3f - todoGlyphHeight);

        foreach (var (summary, complete) in limited)
        {
            if (yOffset + lineHeight > contentRect.Bottom) break;

            var itemIconClass = complete ? completedIcon : pendingIcon;
            var iconBoundsItem = new RectangleF(
                contentRect.X,
                yOffset + 2,
                todoIconSize, todoIconSize);
            utils.DrawFaIcon(image, itemIconClass, iconColor, iconBoundsItem);

            var textX = iconBoundsItem.Right + 6;
            var maxTextH = (todoGlyphHeight + todoExtraSpacing) * 2;
            var textAvailH = Math.Min(maxTextH, contentRect.Bottom - yOffset);
            var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, textAvailH);
            var itemTextColor = complete ? RenderingUtilities.WithOpacity(textColor, 0.6f) : textColor;
            var consumed = utils.DrawWrappedTextEllipsis(image, summary, todoFont, itemTextColor, textRect, maxLines: 2, todoExtraSpacing);

            if (complete && consumed > 0)
            {
                var strikeY = yOffset + consumed / 2f;
                var strikeWidth = Math.Min(
                    TextMeasurer.MeasureSize(summary, new TextOptions(todoFont)).Width,
                    textRect.Width);
                image.Mutate(ctx => ctx.DrawLine(
                    itemTextColor, 1f,
                    new PointF(textX, strikeY),
                    new PointF(textX + strikeWidth, strikeY)));
            }

            yOffset += Math.Max(consumed, lineHeight) + todoItemGap;
        }

        return Task.CompletedTask;
    }
}
