namespace EPaperDashboard.Models.Rendering;

/// <summary>
/// A transient designer render. The revision is supplied by the browser and echoed in the
/// response so a slower, older render can never replace a newer one.
/// </summary>
public sealed record DashboardDesignerPreviewRequest(
    global::EPaperDashboard.Models.LayoutConfig Layout,
    long Revision,
    bool RefreshData = false);

public sealed record DashboardDesignerPreviewResponse(
    long Revision,
    int Width,
    int Height,
    string ContentType,
    string ImageBase64,
    DateTimeOffset RenderedAt,
    IReadOnlyList<WidgetRenderGeometry> Widgets);

public sealed record WidgetRenderGeometry(
    string Id,
    string Type,
    RenderRectangle Bounds,
    RenderRectangle ContentBounds,
    bool Editable,
    IReadOnlyList<EditableWidgetElementGeometry> Elements);

public sealed record EditableWidgetElementGeometry(
    string Id,
    string Kind,
    int? Index,
    RenderRectangle Bounds,
    RenderRectangle Position,
    bool Movable = true,
    bool Resizable = true);

public sealed record RenderRectangle(double X, double Y, double Width, double Height);
