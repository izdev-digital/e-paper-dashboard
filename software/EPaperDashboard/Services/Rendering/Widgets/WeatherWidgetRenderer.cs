using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = SixLabors.ImageSharp.Color;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class WeatherWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "weather";

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

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";

        if (string.IsNullOrEmpty(entityId) || !data.EntityStates.TryGetValue(entityId, out var es))
            return Task.CompletedTask;

        var temperature = RenderingUtilities.GetEntityAttr(es, "temperature") ?? "";
        var condition = es.State ?? "";
        var pressure = RenderingUtilities.GetEntityAttr(es, "pressure") ?? "";

        var items = GetWeatherItems(widget.Config);
        var iconSize = (float)textFontSize;

        foreach (var item in items)
        {
            var visible = item.Visible;
            if (item.Type == "title" && !widget.ShowTitle) visible = false;
            if (!visible) continue;

            var itemRect = new RectangleF(
                contentRect.X + (float)(item.X / 100.0 * contentRect.Width),
                contentRect.Y + (float)(item.Y / 100.0 * contentRect.Height),
                (float)(item.W / 100.0 * contentRect.Width),
                (float)(item.H / 100.0 * contentRect.Height));

            switch (item.Type)
            {
                case "title":
                    TextDrawing.DrawTextEllipsis(image, widget.TitleOverride ?? "Weather", utils.GetFont(titleFontSize, titleFontWeight), titleColor, itemRect);
                    break;
                case "temperature":
                {
                    var tempIcon = item.Icon ?? "fa-temperature-half";
                    var (textX, textW) = DrawWeatherItemIcon(image, tempIcon, iconColor, iconSize, itemRect);
                    TextDrawing.DrawTextEllipsis(image, $"{temperature}°", utils.GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "condition":
                {
                    var condIcon = item.Icon ?? ConditionToIcon(condition);
                    var (textX, textW) = DrawWeatherItemIcon(image, condIcon, iconColor, iconSize, itemRect);
                    TextDrawing.DrawTextEllipsis(image, condition, utils.GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "pressure":
                {
                    var pressIcon = item.Icon ?? "fa-gauge";
                    var (textX, textW) = DrawWeatherItemIcon(image, pressIcon, iconColor, iconSize, itemRect);
                    TextDrawing.DrawTextEllipsis(image, pressure, utils.GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
                case "attribute":
                {
                    var attrKey = item.AttributeKey ?? "humidity";
                    var attrVal = RenderingUtilities.GetEntityAttr(es, attrKey) ?? "";
                    var suffix = attrKey == "humidity" ? "%" : "";
                    var attrIcon = item.Icon ?? attrKey switch
                    {
                        "humidity" => "fa-droplet",
                        "wind_speed" => "fa-wind",
                        _ => "fa-circle-info"
                    };
                    var (textX, textW) = DrawWeatherItemIcon(image, attrIcon, iconColor, iconSize, itemRect);
                    TextDrawing.DrawTextEllipsis(image, $"{attrVal}{suffix}", utils.GetFont(textFontSize, textFontWeight), textColor,
                        new RectangleF(textX, itemRect.Y, textW, itemRect.Height));
                    break;
                }
            }
        }

        return Task.CompletedTask;
    }

    private (float TextX, float TextW) DrawWeatherItemIcon(Image<Rgba32> image, string? icon, Color iconColor, float iconSize, RectangleF itemRect)
    {
        if (!string.IsNullOrEmpty(icon))
        {
            var iconBounds = new RectangleF(
                itemRect.X + 4,
                itemRect.Y + (itemRect.Height - iconSize) / 2f,
                iconSize, iconSize);
            utils.DrawFaIcon(image, icon, iconColor, iconBounds);
            return (iconBounds.Right + 4, itemRect.Width - iconSize - 8);
        }
        return (itemRect.X, itemRect.Width);
    }

    private record WeatherItemEntry(string Type, bool Visible, double X, double Y, double W, double H, string? AttributeKey, string? Label, string? Icon);

    private static List<WeatherItemEntry> GetWeatherItems(JsonElement config)
    {
        var defaults = new List<WeatherItemEntry>
        {
            new("title", true, 0, 0, 100, 20, null, null, null),
            new("temperature", true, 0, 22, 50, 20, null, null, "fa-temperature-half"),
            new("condition", true, 50, 22, 50, 20, null, null, "fa-cloud-sun"),
            new("pressure", true, 0, 44, 50, 20, null, null, "fa-gauge"),
            new("attribute", true, 50, 44, 50, 20, "humidity", "Humidity", "fa-droplet"),
        };

        if (config.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            var result = new List<WeatherItemEntry>();
            foreach (var el in itemsEl.EnumerateArray())
            {
                var type = el.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";
                var visible = !el.TryGetProperty("visible", out var vProp) || vProp.ValueKind != JsonValueKind.False;
                var x = el.TryGetProperty("x", out var xProp) ? xProp.GetDouble() : 0;
                var y = el.TryGetProperty("y", out var yProp) ? yProp.GetDouble() : 0;
                var w = el.TryGetProperty("w", out var wProp) ? wProp.GetDouble() : 100;
                var h = el.TryGetProperty("h", out var hProp) ? hProp.GetDouble() : 20;
                var attrKey = el.TryGetProperty("attributeKey", out var akProp) ? akProp.GetString() : null;
                var label = el.TryGetProperty("label", out var lProp) ? lProp.GetString() : null;
                var icon = el.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                result.Add(new WeatherItemEntry(type, visible, x, y, w, h, attrKey, label, icon));
            }
            return result;
        }

        return defaults;
    }

    private static string ConditionToIcon(string condition) => condition.ToLowerInvariant() switch
    {
        "clear-night" => "fa-moon",
        "cloudy" => "fa-cloud",
        "fog" => "fa-smog",
        "hail" => "fa-cloud-meatball",
        "lightning" => "fa-bolt",
        "lightning-rainy" => "fa-cloud-bolt",
        "partlycloudy" => "fa-cloud-sun",
        "pouring" => "fa-cloud-showers-heavy",
        "rainy" => "fa-cloud-rain",
        "snowy" => "fa-snowflake",
        "snowy-rainy" => "fa-cloud-rain",
        "sunny" => "fa-sun",
        "windy" or "windy-variant" => "fa-wind",
        "exceptional" => "fa-triangle-exclamation",
        _ => "fa-cloud-sun"
    };
}
