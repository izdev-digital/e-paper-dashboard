using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Renders a specific widget type onto an ImageSharp image.
/// Implementations are registered in DI and dispatched by <see cref="DashboardImageRenderingService"/>.
/// </summary>
public interface IWidgetRenderer
{
    /// <summary>The widget type string this renderer handles (e.g. "header", "calendar").</summary>
    string WidgetType { get; }

    /// <summary>
    /// Renders the widget onto the target image within the given content rectangle.
    /// </summary>
    Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect);
}
