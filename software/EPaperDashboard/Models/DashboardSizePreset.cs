namespace EPaperDashboard.Models;

public record DashboardSizePreset(int Width, int Height, string Label)
{
    public static readonly DashboardSizePreset EPaper7_5Inch = new(800, 480, "7.5\" E-Paper (800×480)");

    public static readonly DashboardSizePreset[] All = [EPaper7_5Inch];

    public static DashboardSizePreset Default => EPaper7_5Inch;

    public static bool IsValidSize(int width, int height)
    {
        var w = Math.Max(width, height);
        var h = Math.Min(width, height);
        return All.Any(s => s.Width == w && s.Height == h);
    }

    public static DashboardSizePreset? FindByDimensions(int width, int height)
    {
        var w = Math.Max(width, height);
        var h = Math.Min(width, height);
        return All.FirstOrDefault(s => s.Width == w && s.Height == h);
    }

    public (int Width, int Height) GetEffectiveDimensions(DashboardOrientation orientation) =>
        orientation == DashboardOrientation.Portrait
            ? (Height, Width)
            : (Width, Height);
}
