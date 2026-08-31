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
    Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional capability for renderers whose child elements can be repositioned in the designer.
/// Geometry is returned by the renderer so the interaction overlay follows native output rather
/// than recreating widget layout in HTML.
/// </summary>
public interface IEditableWidgetRenderer : IWidgetRenderer
{
    EditableWidgetRenderPlan BuildRenderPlan(
        WidgetConfigEntry widget,
        RectangleF contentRect);
}
