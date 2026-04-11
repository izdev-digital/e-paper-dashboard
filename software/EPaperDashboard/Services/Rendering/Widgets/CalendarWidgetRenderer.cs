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
        var maxEvents = RenderingUtilities.GetIntProp(widget.Config, "maxEvents") ?? 7;
        var eventGap = RenderingUtilities.GetIntProp(widget.Config, "eventGap") ?? 0;
        var visibleItems = GetCalendarEventItems(widget.Config);

        float yOffset = contentRect.Y;

        if (widget.ShowTitle)
        {
            var titleText = widget.TitleOverride ?? "Events";
            var titleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            utils.DrawTextEllipsis(image, titleText, utils.GetFont(titleFontSize, titleFontWeight), RenderingUtilities.WithOpacity(titleColor, 0.9f), titleRect);
            yOffset += titleFontSize + 6;
        }

        if (!string.IsNullOrEmpty(entityId)
            && data.CalendarEvents.TryGetValue(entityId, out var events)
            && events.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var upcoming = events
                .Where(e =>
                {
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
                var evTextColor = RenderingUtilities.WithOpacity(textColor, 0.85f);
                var evIconColor = RenderingUtilities.WithOpacity(iconColor, 0.85f);

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
                    utils.DrawTextEllipsis(image, text, utils.GetFont(textFontSize, textFontWeight), evTextColor, textRect);
                    yOffset += lineHeight;
                }

                yOffset += eventGap;
            }
        }

        return Task.CompletedTask;
    }

    private record CalendarEventItemEntry(string Type, bool Visible, string? Icon, double X, double Y, double W, double H);

    private static List<CalendarEventItemEntry> GetCalendarEventItems(JsonElement config)
    {
        var defaults = new List<CalendarEventItemEntry>
        {
            new("datetime", true, "fa-clock", 0, 0, 100, 50),
            new("title", true, null, 0, 50, 100, 50),
            new("location", false, "fa-location-dot", 0, 50, 100, 25),
            new("description", false, "fa-align-left", 0, 75, 100, 25),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<CalendarEventItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                var def = defaults.FirstOrDefault(d => d.Type == type) ?? defaults[0];
                var x = el.TryGetProperty("x", out var xP) && xP.TryGetDouble(out var xv) ? xv : def.X;
                var y = el.TryGetProperty("y", out var yP) && yP.TryGetDouble(out var yv) ? yv : def.Y;
                var w = el.TryGetProperty("w", out var wP) && wP.TryGetDouble(out var wv) ? wv : def.W;
                var h = el.TryGetProperty("h", out var hP) && hP.TryGetDouble(out var hv) ? hv : def.H;
                if (visible)
                    result.Add(new CalendarEventItemEntry(type, visible, icon, x, y, w, h));
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
