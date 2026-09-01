using System.Globalization;
using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class CalendarWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "calendar";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var textColor = ctx.TextColor;
        var iconColor = ctx.IconColor;
        var textFontSize = ctx.TextFontSize;
        var textFontWeight = ctx.TextFontWeight;

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";
        var maxEvents = RenderingUtilities.GetIntProp(widget.Config, "maxEvents") ?? 7;
        var eventGap = RenderingUtilities.GetIntProp(widget.Config, "eventGap") ?? 0;
        var visibleItems = GetCalendarEventItems(widget.Config);

        contentRect = WidgetFrameRenderer.DrawOptionalCenteredTitle(
            image, widget, layout, utils, contentRect, "Events");
        float yOffset = contentRect.Y;

        if (!string.IsNullOrEmpty(entityId)
            && data.CalendarEvents.TryGetValue(entityId, out var events)
            && events.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var upcoming = events
                .Where(e =>
                {
                    if (string.IsNullOrEmpty(e.Start) && string.IsNullOrEmpty(e.End))
                        return false;
                    if (DateTimeOffset.TryParse(e.End ?? e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDt))
                        return endDt > now;
                    if (DateTimeOffset.TryParse(e.Start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDt))
                        return startDt >= now;
                    return false;
                })
                .Take(maxEvents).ToList();

            var lineHeight = (int)Math.Ceiling(textFontSize * 1.2f);
            var iconSize = (float)textFontSize;

            foreach (var ev in upcoming)
            {
                if (yOffset + lineHeight > contentRect.Bottom) break;
                var evTextColor = ColorUtils.WithOpacity(textColor, 0.85f);
                var evIconColor = ColorUtils.WithOpacity(iconColor, 0.85f);

                foreach (var item in visibleItems)
                {
                    if (yOffset + lineHeight > contentRect.Bottom) break;

                    string? text = item.Type switch
                    {
                        "datetime" => RenderingUtilities.FormatEventDate(ev.Start),
                        "title" => ev.Summary ?? ev.Description ?? "-",
                        "location" => ev.Location,
                        "description" => ev.Description,
                        _ => null
                    };
                    if (string.IsNullOrEmpty(text)) continue;

                    var itemIcon = item.Icon ?? GetDefaultCalendarEventItemIcon(item.Type);
                    float textX = contentRect.X + 4;

                    if (!string.IsNullOrEmpty(itemIcon))
                    {
                        var iconBounds = new RectangleF(
                            contentRect.X + 4,
                            yOffset + (lineHeight - iconSize) / 2f,
                            iconSize, iconSize);
                        utils.DrawFaIcon(image, itemIcon, evIconColor, iconBounds);
                        textX = iconBounds.Right + 4;
                    }

                    var textRect = new RectangleF(textX, yOffset, contentRect.Right - textX, lineHeight);
                    TextDrawing.DrawTextEllipsis(image, text, utils.GetFont(textFontSize, textFontWeight), evTextColor, textRect);
                    yOffset += lineHeight;
                }

                yOffset += eventGap;
            }
        }

        return Task.CompletedTask;
    }

    private record CalendarEventItemEntry(string Type, bool Visible, string? Icon);

    private static List<CalendarEventItemEntry> GetCalendarEventItems(JsonElement config)
    {
        var defaults = new List<CalendarEventItemEntry>
        {
            new("datetime", true, "fa-clock"),
            new("title", true, null),
            new("location", false, "fa-location-dot"),
            new("description", false, "fa-align-left"),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<CalendarEventItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                if (visible)
                    result.Add(new CalendarEventItemEntry(type, visible, icon));
            }
            return result;
        }

        return defaults.Where(d => d.Visible).ToList();
    }

    private static string GetDefaultCalendarEventItemIcon(string type) => type switch
    {
        "datetime" => "fa-clock",
        "title" => "fa-heading",
        "location" => "fa-location-dot",
        "description" => "fa-align-left",
        _ => ""
    };
}
