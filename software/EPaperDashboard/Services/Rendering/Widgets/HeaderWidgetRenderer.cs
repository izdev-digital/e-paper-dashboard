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

public sealed class HeaderWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "header";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var titleColor = ctx.TitleColor;
        var textColor = ctx.TextColor;
        var iconColor = ctx.IconColor;
        var titleFontSize = ctx.TitleFontSize;
        var textFontSize = ctx.TextFontSize;
        var titleFontWeight = ctx.TitleFontWeight;
        var textFontWeight = ctx.TextFontWeight;

        var title = RenderingUtilities.GetStringProp(widget.Config, "title") ?? "";
        var iconPosition = RenderingUtilities.GetStringProp(widget.Config, "iconPosition") ?? "left";
        var iconSize = RenderingUtilities.GetIntProp(widget.Config, "iconSize") ?? 32;
        var isIconOnLeft = iconPosition != "right";

        if (widget.ShowTitle && !string.IsNullOrEmpty(title))
        {
            var titleX = RenderingUtilities.GetDoubleProp(widget.Config, "titleX") ?? (isIconOnLeft ? 58.0 : 0.0);
            var titleY = RenderingUtilities.GetDoubleProp(widget.Config, "titleY") ?? 0.0;
            var titleW = RenderingUtilities.GetDoubleProp(widget.Config, "titleW") ?? 42.0;
            var titleH = RenderingUtilities.GetDoubleProp(widget.Config, "titleH") ?? 50.0;

            var sectionRect = new RectangleF(
                contentRect.X + (float)(titleX / 100.0 * contentRect.Width),
                contentRect.Y + (float)(titleY / 100.0 * contentRect.Height),
                (float)(titleW / 100.0 * contentRect.Width),
                (float)(titleH / 100.0 * contentRect.Height));

            var effectiveIconSize = Math.Min(iconSize, sectionRect.Height);

            float textLeftOffset = 0;
            float textRightOffset = 0;

            {
                RectangleF iconBounds;
                if (isIconOnLeft)
                {
                    iconBounds = new RectangleF(
                        sectionRect.X,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize, effectiveIconSize);
                    textLeftOffset = effectiveIconSize + 8;
                }
                else
                {
                    iconBounds = new RectangleF(
                        sectionRect.Right - effectiveIconSize,
                        sectionRect.Y + (sectionRect.Height - effectiveIconSize) / 2f,
                        effectiveIconSize, effectiveIconSize);
                    textRightOffset = effectiveIconSize + 8;
                }
                IconDrawing.DrawAppIcon(image, iconColor, iconBounds);

                var dithering = RenderingUtilities.GetBoolProp(widget.Config, "dithering") ?? false;
                if (dithering)
                    RenderingUtilities.DitherRegion(image, layout, iconBounds);
            }

            var titleRect = new RectangleF(
                sectionRect.X + textLeftOffset,
                sectionRect.Y,
                sectionRect.Width - textLeftOffset - textRightOffset,
                sectionRect.Height);

            TextDrawing.DrawTextEllipsis(image, title, utils.GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
        }

        // Render badges
        if (widget.Config.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            int badgeIndex = 0;
            foreach (var badge in badges.EnumerateArray())
            {
                var bEntityId = badge.TryGetProperty("entityId", out var eid) ? eid.GetString() : null;
                var bIcon = badge.TryGetProperty("icon", out var ic) ? ic.GetString() : null;
                bool hasContent = !string.IsNullOrWhiteSpace(bEntityId) || !string.IsNullOrWhiteSpace(bIcon);
                if (!hasContent) { badgeIndex++; continue; }

                var bx = RenderingUtilities.GetBadgeDoubleProp(badge, "x") ?? (badgeIndex % 4) * 22.0;
                var by = RenderingUtilities.GetBadgeDoubleProp(badge, "y") ?? Math.Floor((double)badgeIndex / 4) * 30.0;
                var bw = RenderingUtilities.GetBadgeDoubleProp(badge, "w") ?? 22.0;
                var bh = RenderingUtilities.GetBadgeDoubleProp(badge, "h") ?? 30.0;

                var badgeRect = new RectangleF(
                    contentRect.X + (float)(bx / 100.0 * contentRect.Width),
                    contentRect.Y + (float)(by / 100.0 * contentRect.Height),
                    (float)(bw / 100.0 * contentRect.Width),
                    (float)(bh / 100.0 * contentRect.Height));

                float badgePadding = 4f;
                float textStartX = badgeRect.X + badgePadding;

                if (!string.IsNullOrEmpty(bIcon))
                {
                    var badgeIconSize = textFontSize;
                    var iconBounds = new RectangleF(
                        badgeRect.X + badgePadding,
                        badgeRect.Y + (badgeRect.Height - badgeIconSize) / 2f,
                        badgeIconSize, badgeIconSize);
                    utils.DrawFaIcon(image, bIcon, iconColor, iconBounds);
                    textStartX = iconBounds.Right + 4;
                }

                if (!string.IsNullOrEmpty(bEntityId) && data.EntityStates.TryGetValue(bEntityId, out var es))
                {
                    var badgeText = es.State;
                    var uom = RenderingUtilities.GetEntityAttr(es, "unit_of_measurement");
                    if (!string.IsNullOrEmpty(uom)) badgeText += $" {uom}";
                    var textRect = new RectangleF(textStartX, badgeRect.Y, badgeRect.Right - textStartX - badgePadding, badgeRect.Height);
                    TextDrawing.DrawTextEllipsis(image, badgeText, utils.GetFont(textFontSize, textFontWeight), textColor, textRect);
                }

                badgeIndex++;
            }
        }

        return Task.CompletedTask;
    }
}
