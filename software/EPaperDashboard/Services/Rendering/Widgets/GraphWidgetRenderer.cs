using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.Fonts;
using HorizontalAlignment = EPaperDashboard.Services.Rendering.RenderingUtilities.HorizontalAlignment;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class GraphWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "graph";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var textColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTextColor, o => o?.WidgetTextColor);
        var titleColor = RenderingUtilities.ResolveWidgetColor(widget, layout, c => c.WidgetTitleTextColor, o => o?.WidgetTitleTextColor);
        var textFontSize = layout.TextFontSize > 0 ? layout.TextFontSize : 12;
        var titleFontSize = layout.TitleFontSize > 0 ? layout.TitleFontSize : 15;
        var titleFontWeight = layout.TitleFontWeight > 0 ? layout.TitleFontWeight : 700;
        var gridColorStr = widget.ColorOverrides?.WidgetBorderColor ?? layout.ColorScheme.WidgetBorderColor;

        // Render title if configured — match frontend .graph-title { padding: 8px 12px 4px 12px }
        if (widget.ShowTitle && !string.IsNullOrEmpty(widget.TitleOverride))
        {
            var titleRect = new RectangleF(contentRect.X + 12, contentRect.Y, contentRect.Width - 24, titleFontSize + 8);
            utils.DrawTextCentered(image, widget.TitleOverride, utils.GetFont(titleFontSize, titleFontWeight), titleColor, titleRect);
            contentRect = new RectangleF(contentRect.X, contentRect.Y + titleFontSize + 8, contentRect.Width, contentRect.Height - titleFontSize - 8);
        }

        var plotType = RenderingUtilities.GetStringProp(widget.Config, "plotType") ?? "line";
        var lineWidth = RenderingUtilities.GetIntProp(widget.Config, "lineWidth") ?? 2;
        var barWidth = RenderingUtilities.GetIntProp(widget.Config, "barWidth") ?? 2;

        var seriesList = new List<(string EntityId, string Label, string Color)>();
        if (widget.Config.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var s in series.EnumerateArray())
            {
                var sEntityId = RenderingUtilities.GetStringProp(s, "entityId") ?? "";
                var sLabel = RenderingUtilities.GetStringProp(s, "label") ?? sEntityId;
                var sColor = RenderingUtilities.GetStringProp(s, "color") ?? RenderingUtilities.GetDefaultSeriesColor(layout.ColorScheme, idx);
                if (!string.IsNullOrEmpty(sEntityId))
                    seriesList.Add((sEntityId, sLabel, sColor));
                idx++;
            }
        }

        var hasData = seriesList.Any(s => data.HistoryData.ContainsKey(s.EntityId) && data.HistoryData[s.EntityId].Count > 0);
        if (!hasData)
        {
            utils.DrawCenteredText(image, "Graph", utils.GetFont(textFontSize), titleColor, contentRect);
            return Task.CompletedTask;
        }

        // Collect all data points
        var allValues = new List<double>();
        var allTimestamps = new List<DateTime>();
        foreach (var (entityId, _, _) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states)) continue;
            foreach (var s in states)
            {
                allValues.Add(s.NumericValue);
                allTimestamps.Add(s.LastChanged);
            }
        }

        if (allValues.Count == 0) return Task.CompletedTask;

        var minVal = allValues.Min();
        var maxVal = allValues.Max();
        if (Math.Abs(maxVal - minVal) < 0.001) { minVal -= 1; maxVal += 1; }
        var valRange = maxVal - minVal;

        var minTime = allTimestamps.Min();
        var maxTime = allTimestamps.Max();
        var timeRange = (maxTime - minTime).TotalSeconds;
        if (timeRange < 1) timeRange = 1;

        var padL = Math.Max(35, textFontSize * 4);
        var padR = 10f;
        var padT = 10f;
        var padB = Math.Max(20, textFontSize + 10);
        var plotW = contentRect.Width - padL - padR;
        var plotH = contentRect.Height - padT - padB;
        var originX = contentRect.X + padL;
        var originY = contentRect.Y + padT;

        // Match frontend Chart.js grid: `${widgetBorderColor}20` ≈ 12.5% alpha
        var gridColor = RenderingUtilities.ParseColor(gridColorStr + "20");
        var labelFont = utils.GetFont(Math.Max(8, textFontSize - 2));

        // Grid lines
        image.Mutate(ctx =>
        {
            for (int i = 0; i <= 3; i++)
            {
                var y = originY + plotH * i / 3f;
                ctx.DrawLine(gridColor, 0.5f, new PointF(originX, y), new PointF(originX + plotW, y));

                var val = maxVal - (valRange * i / 3.0);
                var labelRect = new RectangleF(contentRect.X, y - textFontSize / 2f, padL - 4, textFontSize);
                RenderingUtilities.DrawTextAligned(ctx, image, $"{val:F0}", labelFont, textColor, labelRect, HorizontalAlignment.Right);
            }

            // X axis
            ctx.DrawLine(gridColor, 0.5f,
                new PointF(originX, originY + plotH),
                new PointF(originX + plotW, originY + plotH));
        });

        // X axis labels
        for (int i = 0; i <= 4; i++)
        {
            var t = minTime.AddSeconds(timeRange * i / 4.0);
            var x = originX + plotW * i / 4f;
            var labelRect = new RectangleF(x - 20, originY + plotH + 4, 40, textFontSize + 4);
            utils.DrawTextCentered(image, t.ToString("HH:mm"), labelFont, textColor, labelRect);
        }

        // Render series
        foreach (var (entityId, label, color) in seriesList)
        {
            if (!data.HistoryData.TryGetValue(entityId, out var states) || states.Count == 0) continue;
            var seriesColor = RenderingUtilities.ParseColor(color);
            var ordered = states.OrderBy(s => s.LastChanged).ToList();

            if (plotType == "bar")
            {
                var bw = barWidth > 0 ? barWidth * 3f : Math.Max(2, plotW / (ordered.Count + 1));
                image.Mutate(ctx =>
                {
                    foreach (var s in ordered)
                    {
                        var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                        var barX = originX + xFrac * plotW;
                        var yFrac = (float)((s.NumericValue - minVal) / valRange);
                        var barH = yFrac * plotH;
                        var barY = originY + plotH - barH;
                        ctx.Fill(seriesColor, new RectangularPolygon(barX, barY, bw, barH));
                    }
                });
            }
            else
            {
                // Line chart with Catmull-Rom smoothing (matches Chart.js tension: 0.3)
                if (ordered.Count < 2) continue;
                var points = ordered.Select(s =>
                {
                    var xFrac = (float)((s.LastChanged - minTime).TotalSeconds / timeRange);
                    var yFrac = (float)((s.NumericValue - minVal) / valRange);
                    return new PointF(originX + xFrac * plotW, originY + plotH - yFrac * plotH);
                }).ToArray();

                if (points.Length == 2)
                {
                    image.Mutate(ctx => ctx.DrawLine(seriesColor, lineWidth, points));
                }
                else
                {
                    var path = RenderingUtilities.BuildSmoothedPath(points, 0.3f);
                    image.Mutate(ctx => ctx.Draw(seriesColor, lineWidth, path));
                }
            }
        }

        // Legend — match Chart.js: display when >1 series, font size 10, boxWidth 8, padding 8
        if (seriesList.Count > 1)
        {
            var legendFont = utils.GetFont(Math.Max(8, textFontSize - 2));
            var legendY = originY + plotH + padB - 2;
            var legendBoxSize = 8f;
            var legendPadding = 8f;

            var legendItems = seriesList.Select(s =>
            {
                var labelWidth = TextMeasurer.MeasureSize(s.Label, new TextOptions(legendFont)).Width;
                return (s.Label, s.Color, LabelWidth: labelWidth);
            }).ToList();
            var totalLegendWidth = legendItems.Sum(l => legendBoxSize + 4 + l.LabelWidth + legendPadding) - legendPadding;
            var legendX = originX + (plotW - totalLegendWidth) / 2f;

            foreach (var (lLabel, lColor, lWidth) in legendItems)
            {
                var boxColor = RenderingUtilities.ParseColor(lColor);
                image.Mutate(ctx => ctx.Fill(boxColor, new RectangularPolygon(
                    legendX, legendY, legendBoxSize, legendBoxSize)));
                legendX += legendBoxSize + 4;
                var lRect = new RectangleF(legendX, legendY - 1, lWidth + 2, legendBoxSize + 2);
                utils.DrawTextEllipsis(image, lLabel, legendFont, textColor, lRect);
                legendX += lWidth + legendPadding;
            }
        }

        return Task.CompletedTask;
    }
}
