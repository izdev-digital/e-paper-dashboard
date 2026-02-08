using System.Text.Json;

namespace EPaperDashboard.Models
{
    public class LayoutConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int GridCols { get; set; }
        public int GridRows { get; set; }
        public ColorScheme ColorScheme { get; set; } = new();
        public List<WidgetConfig> Widgets { get; set; } = new();
        public int CanvasPadding { get; set; }
        public int WidgetGap { get; set; }
        public int WidgetBorder { get; set; }
        public int TitleFontSize { get; set; }
        public int TextFontSize { get; set; }
        public int TitleFontWeight { get; set; }
        public int TextFontWeight { get; set; }
    }

    public class ColorScheme
    {
        public string Name { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public List<string> Palette { get; set; } = new();
        public string Background { get; set; } = string.Empty;
        public string CanvasBackgroundColor { get; set; } = string.Empty;
        public string WidgetBackgroundColor { get; set; } = string.Empty;
        public string WidgetBorderColor { get; set; } = string.Empty;
        public string WidgetTitleTextColor { get; set; } = string.Empty;
        public string WidgetTextColor { get; set; } = string.Empty;
        public string IconColor { get; set; } = string.Empty;
        public string Foreground { get; set; } = string.Empty;
        public string Accent { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class WidgetConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public WidgetPosition Position { get; set; } = new();
        public JsonElement Config { get; set; }
        public WidgetColorOverrides? ColorOverrides { get; set; }
        public string? TitleOverride { get; set; }
    }

    public class WidgetPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
    }

    public class WidgetColorOverrides
    {
        public string? WidgetBackgroundColor { get; set; }
        public string? WidgetBorderColor { get; set; }
        public string? WidgetTitleTextColor { get; set; }
        public string? WidgetTextColor { get; set; }
        public string? IconColor { get; set; }
    }
}
