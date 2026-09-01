using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class WeatherForecastWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "weather-forecast";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var textColor = ctx.TextColor;
        var textFontSize = ctx.TextFontSize;
        var textFontWeight = ctx.TextFontWeight;

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";
        var forecastMode = RenderingUtilities.GetStringProp(widget.Config, "forecastMode") ?? "daily";
        var maxItems = RenderingUtilities.GetIntProp(widget.Config, "maxItems");
        var visibleFields = RenderingUtilities.GetStringArrayProp(widget.Config, "visibleFields") ?? new[] { "time", "condition", "tempHigh", "tempLow" };
        if (visibleFields.Contains("temperature"))
            visibleFields = visibleFields.Where(f => f != "temperature").Concat(new[] { "tempHigh", "tempLow" }).Distinct().ToArray();
        var rowGap = RenderingUtilities.GetIntProp(widget.Config, "rowGap") ?? 0;

        var isTinyMode = widget.Position.W <= 2 || widget.Position.H == 1;
        if (!isTinyMode)
            contentRect = WidgetFrameRenderer.DrawOptionalCenteredTitle(
                image, widget, layout, utils, contentRect, "Forecast");
        float yOffset = contentRect.Y;

        var forecastKey = WeatherForecastDataKey.Create(entityId, forecastMode);
        if (string.IsNullOrEmpty(entityId)
            || !data.WeatherForecasts.TryGetValue(forecastKey, out var forecastList)
            || forecastList.Count == 0)
        {
            return Task.CompletedTask;
        }

        var w = widget.Position.W;
        var h = widget.Position.H;
        var itemCount = maxItems ?? GetDefaultMaxItems(w, h, forecastMode);
        var sourceItems = forecastMode == "hourly"
            ? FilterHourlyForecast(forecastList)
            : forecastList;
        var items = sourceItems.Take(itemCount).ToList();

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
            var item = items[i];
            var colX = contentRect.X + i * (colWidth + colGap);
            float itemY = yOffset;

            var dt = item.Datetime ?? "";
            if (visibleFields.Contains("time"))
            {
                var timeRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                TextDrawing.DrawTextCentered(image, RenderingUtilities.FormatForecastTime(dt, forecastMode), utils.GetFont(textFontSize, textFontWeight), textColor, timeRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("condition") && !isTinyMode && widget.Position.H > 2)
            {
                var condStr = RenderingUtilities.FormatCondition(item.Condition);
                var condRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                TextDrawing.DrawTextCentered(image, condStr, utils.GetFont(textFontSize, textFontWeight), textColor, condRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempHigh"))
            {
                var temp = item.Temperature is not null ? RenderingUtilities.RoundNum(item.Temperature) : "";
                var tempRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                TextDrawing.DrawTextCentered(image, $"{temp}{tempUnit}", utils.GetFont(textFontSize, textFontWeight), textColor, tempRect);
                itemY += lineHeight + rowGap;
            }

            if (visibleFields.Contains("tempLow") && forecastMode != "hourly")
            {
                var tempLow = item.TempLow is not null ? RenderingUtilities.RoundNum(item.TempLow) : "";
                if (!string.IsNullOrEmpty(tempLow))
                {
                    var tlRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    TextDrawing.DrawTextCentered(image, $"{tempLow}{tempUnit}", utils.GetFont(textFontSize, textFontWeight), ColorUtils.WithOpacity(textColor, 0.7f), tlRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("precipitation"))
            {
                var precip = item.PrecipitationProbability is not null
                    ? RenderingUtilities.RoundNum(item.PrecipitationProbability)
                    : null;
                if (!string.IsNullOrEmpty(precip))
                {
                    var precipRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    TextDrawing.DrawTextCentered(image, $"{precip}%", utils.GetFont(textFontSize, textFontWeight), textColor, precipRect);
                    itemY += lineHeight + rowGap;
                }
            }

            if (visibleFields.Contains("wind"))
            {
                var windSpeed = item.WindSpeed is not null
                    ? RenderingUtilities.RoundNumOneDecimal(item.WindSpeed)
                    : null;
                if (!string.IsNullOrEmpty(windSpeed))
                {
                    var windUnit = data.EntityStates.TryGetValue(entityId, out var wes) ? RenderingUtilities.GetEntityAttr(wes, "wind_speed_unit") ?? "" : "";
                    var windRect = new RectangleF(colX, itemY, colWidth, lineHeight);
                    TextDrawing.DrawTextCentered(image, $"{windSpeed} {windUnit}", utils.GetFont(textFontSize, textFontWeight), textColor, windRect);
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

    private static List<WeatherForecast> FilterHourlyForecast(List<WeatherForecast> forecast)
    {
        if (forecast.Count < 2)
            return forecast;

        var filtered = new List<WeatherForecast> { forecast[0] };
        if (!TryGetForecastDate(forecast[0], out var lastDate))
            return forecast;

        foreach (var item in forecast.Skip(1))
        {
            if (!TryGetForecastDate(item, out var currentDate))
                continue;

            if (currentDate - lastDate >= TimeSpan.FromHours(1))
            {
                filtered.Add(item);
                lastDate = currentDate;
            }
        }

        return filtered;
    }

    private static bool TryGetForecastDate(WeatherForecast item, out DateTimeOffset date)
    {
        date = default;
        return DateTimeOffset.TryParse(item.Datetime, out date);
    }
}
