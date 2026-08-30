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

public sealed class HeaderWidgetRenderer(RenderingUtilities utils) : IEditableWidgetRenderer
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

            var sectionRect = ToRectangle(CreateElement(
                "title", "title", null, titleX, titleY, titleW, titleH, contentRect).Bounds);

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

                var badgeRect = ToRectangle(CreateElement(
                    $"badge-{badgeIndex}", "badge", badgeIndex,
                    bx, by, bw, bh, contentRect).Bounds);

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

    public IReadOnlyList<EditableWidgetElementGeometry> GetEditableElements(
        WidgetConfigEntry widget,
        RectangleF contentRect)
    {
        var result = new List<EditableWidgetElementGeometry>();
        var iconPosition = RenderingUtilities.GetStringProp(widget.Config, "iconPosition") ?? "left";
        var titleX = RenderingUtilities.GetDoubleProp(widget.Config, "titleX") ?? (iconPosition != "right" ? 58.0 : 0.0);
        var titleY = RenderingUtilities.GetDoubleProp(widget.Config, "titleY") ?? 0.0;
        var titleW = RenderingUtilities.GetDoubleProp(widget.Config, "titleW") ?? 42.0;
        var titleH = RenderingUtilities.GetDoubleProp(widget.Config, "titleH") ?? 50.0;

        if (widget.ShowTitle)
            result.Add(CreateElement("title", "title", null, titleX, titleY, titleW, titleH, contentRect));

        if (widget.Config.TryGetProperty("badges", out var badges) && badges.ValueKind == JsonValueKind.Array)
        {
            var badgeIndex = 0;
            foreach (var badge in badges.EnumerateArray())
            {
                var entityId = badge.TryGetProperty("entityId", out var entity) ? entity.GetString() : null;
                var icon = badge.TryGetProperty("icon", out var iconProperty) ? iconProperty.GetString() : null;
                if (!string.IsNullOrWhiteSpace(entityId) || !string.IsNullOrWhiteSpace(icon))
                {
                    var x = RenderingUtilities.GetBadgeDoubleProp(badge, "x") ?? (badgeIndex % 4) * 22.0;
                    var y = RenderingUtilities.GetBadgeDoubleProp(badge, "y") ?? Math.Floor((double)badgeIndex / 4) * 30.0;
                    var w = RenderingUtilities.GetBadgeDoubleProp(badge, "w") ?? 22.0;
                    var h = RenderingUtilities.GetBadgeDoubleProp(badge, "h") ?? 30.0;
                    result.Add(CreateElement(
                        $"badge-{badgeIndex}", "badge", badgeIndex,
                        x, y, w, h, contentRect));
                }
                badgeIndex++;
            }
        }

        return result;
    }

    private static EditableWidgetElementGeometry CreateElement(
        string id,
        string kind,
        int? index,
        double x,
        double y,
        double width,
        double height,
        RectangleF contentRect)
    {
        var position = new RenderRectangle(x, y, width, height);
        var bounds = new RenderRectangle(
            contentRect.X + x / 100.0 * contentRect.Width,
            contentRect.Y + y / 100.0 * contentRect.Height,
            width / 100.0 * contentRect.Width,
            height / 100.0 * contentRect.Height);
        return new EditableWidgetElementGeometry(id, kind, index, bounds, position);
    }

    private static RectangleF ToRectangle(RenderRectangle rectangle) => new(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)rectangle.Width,
        (float)rectangle.Height);
}
