using EPaperDashboard.Models;
using static EPaperDashboard.Services.Ai.JsonElementHelpers;

namespace EPaperDashboard.Services.Ai;

public sealed class WidgetLayoutEngine
{
    public void ComputeSizes(
        List<WidgetConfig> widgets,
        AiDataSnapshot aiData,
        LayoutConfig layoutConfig,
        int gridCols)
    {
        var metrics = ComputeLayoutMetrics(layoutConfig, gridCols);

        foreach (var widget in widgets)
        {
            var (w, h) = ComputeIdealSize(widget, aiData, metrics, gridCols);
            widget.Position.W = w;
            widget.Position.H = h;
        }
    }

    private static (int cellWidth, int cellHeight, int charsPerCellWidth, double linesPerCell, int textLineHeight, int titleLineHeight)
        ComputeLayoutMetrics(LayoutConfig layoutConfig, int gridCols)
    {
        var (width, height) = (layoutConfig.Width, layoutConfig.Height);
        if (width <= 0)
        {
            width = 800;
        }
        if (height <= 0)
        {
            height = 480;
        }

        var canvasPadding = layoutConfig.CanvasPadding > 0 ? layoutConfig.CanvasPadding : 8;
        var widgetGap = layoutConfig.WidgetGap > 0 ? layoutConfig.WidgetGap : 8;
        var widgetPadding = layoutConfig.WidgetPadding > 0 ? layoutConfig.WidgetPadding : 8;
        var widgetBorder = layoutConfig.WidgetBorder >= 0 ? layoutConfig.WidgetBorder : 1;
        var titleFontSize = layoutConfig.TitleFontSize > 0 ? layoutConfig.TitleFontSize : 14;
        var textFontSize = layoutConfig.TextFontSize > 0 ? layoutConfig.TextFontSize : 12;
        var gridRows = layoutConfig.GridRows > 0 ? layoutConfig.GridRows : 8;

        var usableWidth = width - (2 * canvasPadding) - ((gridCols - 1) * widgetGap);
        var usableHeight = height - (2 * canvasPadding) - ((gridRows - 1) * widgetGap);
        var cellWidth = usableWidth / gridCols;
        var cellHeight = usableHeight / gridRows;

        var innerPadding = 2 * (widgetPadding + widgetBorder);
        var titleLineHeight = (int)(titleFontSize * 1.4);
        var textLineHeight = (int)(textFontSize * 1.4);
        var charsPerCellWidth = (int)((cellWidth - innerPadding) / (textFontSize * 0.55));
        var linesPerCell = (double)(cellHeight - innerPadding) / textLineHeight;

        return (cellWidth, cellHeight, Math.Max(1, charsPerCellWidth), Math.Max(1, linesPerCell), textLineHeight, titleLineHeight);
    }

    private static (int w, int h) ComputeIdealSize(
        WidgetConfig widget,
        AiDataSnapshot aiData,
        (int cellWidth, int cellHeight, int charsPerCellWidth, double linesPerCell, int textLineHeight, int titleLineHeight) metrics,
        int gridCols)
    {
        var config = widget.Config;

        switch (widget.Type)
        {
            case "header":
                return (gridCols, 1);

            case "app-icon":
                return (1, 1);

            case "weather":
                return (Math.Min(4, gridCols), 2);

            case "weather-forecast":
            {
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                return (w, 3);
            }

            case "calendar":
            {
                var entityId = GetStringProp(config, "entityId") ?? "";
                var eventCount = aiData.CalendarEvents.TryGetValue(entityId, out var events) ? events.Count : 0;
                var maxEvents = GetIntProp(config, "maxEvents") ?? 7;
                var dataRows = Math.Min(maxEvents, eventCount);
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                var contentLines = dataRows;
                var h = 1 + (int)Math.Ceiling(contentLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            case "todo":
            {
                var entityId = GetStringProp(config, "entityId") ?? "";
                var itemCount = aiData.TodoItems.TryGetValue(entityId, out var items) ? items.Count : 0;
                var maxItems = GetIntProp(config, "maxItems") ?? 50;
                var dataRows = Math.Min(maxItems, itemCount);
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                var contentLines = (int)Math.Ceiling(dataRows * 1.3);
                var h = 1 + (int)Math.Ceiling(contentLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            case "rss-feed":
            {
                var w = Math.Min(4, Math.Max(3, gridCols / 3));
                return (w, 4);
            }

            case "graph":
            {
                var w = Math.Min(6, Math.Max(4, gridCols / 2));
                return (w, 3);
            }

            case "markdown":
            case "ai-content":
            {
                var content = GetStringProp(config, "content") ?? "";
                var charCount = content.Length;
                int w;
                if (charCount < 100)
                {
                    w = Math.Min(gridCols, Math.Max(3, gridCols / 3));
                }
                else if (charCount < 300)
                {
                    w = Math.Min(gridCols, Math.Max(4, gridCols / 2));
                }
                else
                {
                    w = Math.Min(gridCols, Math.Max(6, gridCols * 2 / 3));
                }

                var charsPerLine = metrics.charsPerCellWidth * w;
                var textLines = (int)Math.Ceiling((double)charCount / charsPerLine);
                var newlineCount = content.Count(c => c == '\n');
                textLines = Math.Max(textLines, newlineCount + 1);
                var h = 1 + (int)Math.Ceiling(textLines / metrics.linesPerCell);
                return (w, Math.Max(2, h));
            }

            default:
                return (Math.Min(4, gridCols), 2);
        }
    }
}
