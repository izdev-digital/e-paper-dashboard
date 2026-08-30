using System.Text.Json;

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
    string ImageUrl,
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
    EditableElementLayoutBinding? LayoutBinding = null,
    string? Label = null,
    bool Movable = true,
    bool Resizable = true);

/// <summary>
/// JSON Pointer paths into the transient layout. The browser applies these bindings generically,
/// so adding an editable renderer does not require widget-type-specific persistence code.
/// </summary>
public sealed record EditableElementLayoutBinding(
    string XPath,
    string YPath,
    string WidthPath,
    string HeightPath,
    JsonElement? SeedConfig = null);

/// <summary>
/// Renderer-owned layout plan shared by native drawing and designer geometry.
/// </summary>
public sealed record EditableWidgetRenderPlan(
    IReadOnlyList<EditableWidgetElementGeometry> Elements);

public sealed record RenderRectangle(double X, double Y, double Width, double Height);
