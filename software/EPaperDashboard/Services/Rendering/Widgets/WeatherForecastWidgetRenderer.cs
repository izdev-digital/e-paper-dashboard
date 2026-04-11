using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class WeatherForecastWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "weather-forecast";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var titleColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var textFontWeight = layout.TextFontWeight > 0 ? layout.TextFontWeight : 400;

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";
        var forecastMode = RenderingUtilities.GetStringProp(widget.Config, "forecastMode") ?? "daily";
        var maxItems = RenderingUtilities.GetIntProp(widget.Config, "maxItems");
        var visibleFields = RenderingUtilities.GetStringArrayProp(widget.Config, "visibleFields") ?? new[] { "time", "condition", "tempHigh", "tempLow" };
        if (visibleFields.Contains("temperature"))
            visibleFields = visibleFields.Where(f => f != "temperature").Concat(new[] { "tempHigh", "tempLow" }).Distinct().ToArray();
        var rowGap = RenderingUtilities.GetIntProp(widget.Config, "rowGap") ?? 0;

        float yOffset = contentRect.Y;

        var isTinyMode = widget.Position.W <= 2 || widget.Position.H == 1;
        if (widget.ShowTitle && !isTinyMode)
        {
            var headerRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, titleFontSize + 4);
            utils.DrawTextEllipsis(image, widget.TitleOverride ?? "Forecast", utils.GetFont(titleFontSize, titleFontWeight), titleColor, headerRect);
            yOffset += titleFontSize + 7;
        }

        if (string.IsNullOrEmpty(entityId)
            || !data.WeatherForecasts.TryGetValue(entityId, out var forecastList)
            || forecastList.Count == 0)
        {
            return Task.CompletedTask;
        }

        var w = widget.Position.W;
        var h = widget.Position.H;
        var itemCount = maxItems ?? GetDefaultMaxItems(w, h, forecastMode);
        var items = forecastList.Take(itemCount).ToList();

        var tempUnit = "°C";
        if (data.EntityStates.TryGetValue(entityId, out var es))
            tempUnit = RenderingUtilities.GetEntityAttr(es, "temperature_unit") ?? "°C";

        if (items.Count == 0) return Task.CompletedTask;
        var colGap = 2f;
        var totalGaps = colGap * (items.Count - 1);
        var colWidth = (contentRect.Width - totalGaps) / items.Count;
        var lineHeight = (int)Math.Ceiling(textFontSize * 1.2f);

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is not Dictionary<string, object?> dict) continue;
            var colX = contentRect.X + i * (colWidth + colGap);
            float itemY = yOffset;

            var dt = dict.TryGetValue("datetime", out var dtVal) ? dtVal?.ToString() : "";
            if (visibleFields.Contains("time"))
            {
                var timeRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                utils.DrawTextCentered(image, RenderingUtilities.FormatForecastTime(dt, forecastMode), utils.GetFont(textFontSize, textFontWeight), textColor, timeRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("condition") && !isTinyMode && widget.Position.H > 2)
            {
                var condStr = dict.TryGetValue("condition", out var cv) ? RenderingUtilities.FormatCondition(cv?.ToString()) : "";
                var condRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                utils.DrawTextCentered(image, condStr, utils.GetFont(textFontSize, textFontWeight), textColor, condRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempHigh"))
            {
                var temp = dict.TryGetValue("temperature", out var tVal) ? RenderingUtilities.RoundNum(tVal) : "";
                var tempRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                utils.DrawTextCentered(image, $"{temp}{tempUnit}", utils.GetFont(textFontSize, textFontWeight), textColor, tempRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempLow") && forecastMode != "hourly")
            {
                var tempLow = dict.TryGetValue("templow", out var tlVal) ? RenderingUtilities.RoundNum(tlVal) : "";
                if (!string.IsNullOrEmpty(tempLow))
                {
                    var tlRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    utils.DrawTextCentered(image, $"{tempLow}{tempUnit}", utils.GetFont(textFontSize, textFontWeight), RenderingUtilities.WithOpacity(textColor, 0.7f), tlRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("precipitation"))
            {
                var precip = dict.TryGetValue("precipitation_probability", out var ppVal) ? RenderingUtilities.RoundNum(ppVal) : null;
                if (!string.IsNullOrEmpty(precip))
                {
                    var precipRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    utils.DrawTextCentered(image, $"{precip}%", utils.GetFont(textFontSize, textFontWeight), textColor, precipRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("wind"))
            {
                var windSpeed = dict.TryGetValue("wind_speed", out var wsVal) ? RenderingUtilities.RoundNumOneDecimal(wsVal) : null;
                if (!string.IsNullOrEmpty(windSpeed))
                {
                    var windUnit = data.EntityStates.TryGetValue(entityId, out var wes) ? RenderingUtilities.GetEntityAttr(wes, "wind_speed_unit") ?? "" : "";
                    var windRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    utils.DrawTextCentered(image, $"{windSpeed} {windUnit}", utils.GetFont(textFontSize, textFontWeight), textColor, windRect);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static int GetDefaultMaxItems(int w, int h, string mode)
    {
        if (w == 1 && h == 1) return 0;
        if (h == 1) return mode switch
        {
            "hourly" => Math.Min(4, w * 2),
            "daily" => Math.Min(2, w),
            "weekly" => 1,
            _ => 2
        };
        if (h == 2) return mode switch
        {
            "hourly" => w switch { 1 => 3, 2 => 5, _ => 7 },
            "daily" => w switch { 1 => 2, 2 => 3, _ => 4 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 3 },
            _ => 3
        };
        return mode switch
        {
            "hourly" => w switch { 1 => 4, 2 => 6, _ => 8 },
            "daily" => w switch { 1 => 2, 2 => 4, _ => 5 },
            "weekly" => w switch { 1 => 1, 2 => 2, _ => 4 },
            _ => 3
        };
    }
}
