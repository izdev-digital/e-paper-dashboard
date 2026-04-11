using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Ai;

public sealed class GridPacker(ILogger<GridPacker> logger)
{
    public List<WidgetConfig> Pack(
        List<WidgetConfig> widgets,
        List<WidgetConfig> pinnedWidgets,
        int gridCols,
        int gridRows)
    {
        var grid = new bool[gridCols, gridRows];

        foreach (var pinned in pinnedWidgets)
        {
            MarkCells(grid, pinned.Position, gridCols, gridRows);
        }

        var placed = new List<WidgetConfig>();

        foreach (var widget in widgets)
        {
            var idealW = widget.Position.W;
            var idealH = widget.Position.H;

            if (TryPlace(grid, widget, idealW, idealH, gridCols, gridRows))
            {
                placed.Add(widget);
                continue;
            }

            var placed2 = false;
            for (var h = idealH - 1; h >= 1; h--)
            {
                if (TryPlace(grid, widget, idealW, h, gridCols, gridRows))
                {
                    placed2 = true;
                    placed.Add(widget);
                    break;
                }
            }
            if (placed2)
            {
                continue;
            }

            for (var w = idealW - 1; w >= 1; w--)
            {
                for (var h = idealH; h >= 1; h--)
                {
                    if (TryPlace(grid, widget, w, h, gridCols, gridRows))
                    {
                        placed2 = true;
                        placed.Add(widget);
                        break;
                    }
                }
                if (placed2)
                {
                    break;
                }
            }

            if (!placed2)
            {
                logger.LogInformation(
                    "Widget '{Id}' ({Type}, {W}×{H}) could not fit on the grid, skipping",
                    widget.Id, widget.Type, idealW, idealH);
            }
        }

        return placed;
    }

    private static bool TryPlace(
        bool[,] grid, WidgetConfig widget,
        int w, int h,
        int gridCols, int gridRows)
    {
        for (var row = 0; row <= gridRows - h; row++)
        {
            for (var col = 0; col <= gridCols - w; col++)
            {
                var pos = new WidgetPosition { X = col, Y = row, W = w, H = h };
                if (CanPlace(grid, pos, gridCols, gridRows))
                {
                    widget.Position = pos;
                    MarkCells(grid, pos, gridCols, gridRows);
                    return true;
                }
            }
        }
        return false;
    }

    private static bool CanPlace(bool[,] grid, WidgetPosition pos, int gridCols, int gridRows)
    {
        if (pos.X + pos.W > gridCols || pos.Y + pos.H > gridRows)
        {
            return false;
        }

        for (var row = pos.Y; row < pos.Y + pos.H; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W; col++)
            {
                if (grid[col, row])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void MarkCells(bool[,] grid, WidgetPosition pos, int gridCols, int gridRows)
    {
        for (var row = pos.Y; row < pos.Y + pos.H && row < gridRows; row++)
        {
            for (var col = pos.X; col < pos.X + pos.W && col < gridCols; col++)
            {
                grid[col, row] = true;
            }
        }
    }
}
