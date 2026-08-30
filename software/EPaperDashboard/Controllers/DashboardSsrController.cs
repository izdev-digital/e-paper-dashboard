using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace EPaperDashboard.Controllers;

/// <summary>
/// Serves dashboards as server-side rendered images.
/// Uses the same cookie auth as the Angular frontend.
/// </summary>
[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardSsrController(
    DashboardService dashboardService,
    DashboardImageRenderingService dashboardImageRenderingService,
    IPageToImageRenderingService renderingService,
    IDeploymentStrategy deploymentStrategy,
    IEnvironmentConfiguration environmentConfiguration,
    TimeProvider timeProvider) : BaseApiController
{
    /// <summary>
    /// Returns the dashboard rendered directly to an image using ImageSharp.
    /// </summary>
    [HttpGet("{id}/render-image")]
    public async Task<IActionResult> RenderDashboardImage(
        string id,
        [FromQuery] string format = "jpeg",
        [FromQuery] bool refresh = false)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
        {
            return BadRequest("Invalid dashboard ID");
        }

        var dashboard = dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
            return NotFound("Dashboard not found");

        if (dashboard.Value.UserId != CurrentUserId)
            return Forbid();

        if (dashboard.Value.LayoutConfig == null)
            return BadRequest("Dashboard has no layout configuration. Open the designer and create a layout first.");

        try
        {
            var layoutToRender = dashboard.Value.GetMergedLayoutConfig();

            using var rawImage = await dashboardImageRenderingService.RenderDashboardImageAsync(
                dashboard.Value.Id.ToString(),
                layoutToRender,
                HttpContext.RequestAborted,
                bypassCache: refresh);

            using IImage image = ImageAdapter<SixLabors.ImageSharp.PixelFormats.Rgba32>.Wrap(rawImage);

            var (contentType, encoder) = GetEncoder(format);
            return await ConvertToResult(image, encoder, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to render dashboard image: {ex.Message}");
        }
    }

    /// <summary>
    /// Renders a transient layout without persisting it. The designer uses this endpoint so the
    /// rendered preview includes the user's current unsaved changes.
    /// </summary>
    [HttpPost("{id}/render-image")]
    public async Task<IActionResult> RenderTransientDashboardImage(
        string id,
        [FromBody] EPaperDashboard.Models.LayoutConfig layout,
        [FromQuery] string format = "png",
        [FromQuery] bool refresh = true)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
            return BadRequest("Invalid dashboard ID");

        var dashboard = dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
            return NotFound("Dashboard not found");

        if (dashboard.Value.UserId != CurrentUserId)
            return Forbid();

        var validationError = ValidateTransientLayout(layout);
        if (validationError is not null)
            return BadRequest(validationError);

        try
        {
            var layoutToRender = dashboard.Value.GetMergedLayoutConfig(layout);
            using var rawImage = await dashboardImageRenderingService.RenderDashboardImageAsync(
                dashboard.Value.Id.ToString(),
                layoutToRender,
                HttpContext.RequestAborted,
                bypassCache: refresh);
            using IImage image = ImageAdapter<SixLabors.ImageSharp.PixelFormats.Rgba32>.Wrap(rawImage);

            var (contentType, encoder) = GetEncoder(format);
            return await ConvertToResult(image, encoder, contentType);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to render dashboard image: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves all data needed by a transient designer layout through the production data
    /// collector. The designer consumes this snapshot instead of calling each source separately.
    /// </summary>
    [HttpPost("{id}/preview-data")]
    public async Task<IActionResult> GetTransientPreviewData(
        string id,
        [FromBody] EPaperDashboard.Models.LayoutConfig layout)
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
            return BadRequest("Invalid dashboard ID");

        var dashboard = dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
            return NotFound("Dashboard not found");

        if (dashboard.Value.UserId != CurrentUserId)
            return Forbid();

        var validationError = ValidateTransientLayout(layout);
        if (validationError is not null)
            return BadRequest(validationError);

        var layoutToResolve = dashboard.Value.GetMergedLayoutConfig(layout);
        var data = await dashboardImageRenderingService.FetchDashboardDataAsync(
            dashboard.Value.Id.ToString(),
            layoutToResolve,
            HttpContext.RequestAborted,
            bypassCache: true);

        return Ok(DashboardPreviewData.FromSsrData(data, timeProvider));
    }

    /// <summary>
    /// Renders a preview of the dashboard. Supports both Custom (ImageSharp) and
    /// HomeAssistant (Playwright) rendering modes. Protected by cookie auth.
    /// </summary>
    [HttpGet("{id}/preview")]
    public async Task<IActionResult> PreviewDashboard(string id, [FromQuery] string format = "png")
    {
        if (!DashboardId.TryParse(id, out var dashboardId))
            return BadRequest("Invalid dashboard ID");

        var dashboard = dashboardService.GetDashboardById(dashboardId);
        if (dashboard.HasNoValue)
            return NotFound("Dashboard not found");

        if (dashboard.Value.UserId != CurrentUserId)
            return Forbid();

        var (width, height) = dashboard.Value.GetEffectiveSize();
        var imageSize = new Size(width, height);

        return dashboard.Value.RenderingMode == RenderingMode.Custom
            ? await RenderCustomPreview(dashboard.Value, imageSize, format)
            : await RenderHomeAssistantPreview(dashboard.Value, imageSize, format);
    }

    private async Task<IActionResult> RenderCustomPreview(Dashboard dashboard, Size imageSize, string format)
    {
        if (dashboard.LayoutConfig == null)
            return BadRequest("Dashboard has no layout configuration. Open the designer and create a layout first.");

        try
        {
            var layoutToRender = dashboard.GetMergedLayoutConfig();

            using var rawImage = await dashboardImageRenderingService.RenderDashboardImageAsync(
                dashboard.Id.ToString(),
                layoutToRender,
                HttpContext.RequestAborted);

            using IImage image = ImageAdapter<SixLabors.ImageSharp.PixelFormats.Rgba32>.Wrap(rawImage);

            var (contentType, encoder) = GetEncoder(format);
            return await ConvertToResult(image, encoder, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to render dashboard image: {ex.Message}");
        }
    }

    private async Task<IActionResult> RenderHomeAssistantPreview(Dashboard dashboard, Size imageSize, string format)
    {
        var dashboardInfo = GetDashboardInfo(dashboard);
        if (dashboardInfo.HasNoValue)
        {
            var hint = deploymentStrategy.IsAutoConnected
                ? "Dashboard configuration incomplete. The 'Home Assistant Dashboard' rendering mode requires " +
                  "a Long-Lived Access Token. Create one in Home Assistant (Profile → Long-Lived Access Tokens) " +
                  "and set it on the dashboard."
                : "Dashboard configuration incomplete. Ensure Host, Path, and Access Token are set.";
            return NotFound(hint);
        }

        var (contentType, encoder) = GetEncoder(format);
        var authStrategy = new HassAuthStrategy(dashboardInfo.Value.Tokens);

        var result = await renderingService
            .RenderDashboardAsync(dashboardInfo.Value.DashboardUri, imageSize, authStrategy);

        return await result.Match(
            image => ConvertToResult(image, encoder, contentType),
            error => Task.FromResult<IActionResult>(BadRequest(error)));
    }

    private Maybe<(Uri DashboardUri, HassTokens Tokens)> GetDashboardInfo(Dashboard dashboard)
    {
        var (strategyHost, _) = deploymentStrategy.GetHomeAssistantConnection(dashboard);

        var host = dashboard.Host;
        if (string.IsNullOrWhiteSpace(host) && deploymentStrategy.Mode != DeploymentMode.Standalone)
        {
            host = deploymentStrategy.Mode == DeploymentMode.Addon
                ? Constants.HomeAssistantInternalUrl
                : strategyHost;
        }

        var accessToken = dashboard.AccessToken;

        if (string.IsNullOrWhiteSpace(accessToken)
            || !Uri.TryCreate(host, UriKind.Absolute, out var hostUri)
            || !Uri.TryCreate(dashboard.Path, UriKind.Relative, out var pathUri))
        {
            return Maybe.None;
        }

        var hassUrl = hostUri.AbsoluteUri.TrimEnd('/');
        var clientId = environmentConfiguration.ClientUri?.AbsoluteUri.TrimEnd('/') ?? hassUrl;

        return (new Uri(hostUri, pathUri), new HassTokens(accessToken, "Bearer", hassUrl, clientId));
    }

    private async Task<IActionResult> ConvertToResult(IImage image, IImageEncoder encoder, string contentType)
    {
        var outStream = new MemoryStream();
        await image.SaveAsync(outStream, encoder);
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, contentType);
    }

    private static string? ValidateTransientLayout(EPaperDashboard.Models.LayoutConfig layout)
    {
        if (layout.Width is < 1 or > 4096 || layout.Height is < 1 or > 4096)
            return "Dashboard dimensions must be between 1 and 4096 pixels.";
        if (layout.GridCols is < 1 or > 100 || layout.GridRows is < 1 or > 100)
            return "Dashboard grid dimensions must be between 1 and 100.";
        if (layout.Widgets.Count > 500)
            return "Dashboard cannot contain more than 500 widgets.";

        return null;
    }

    private static (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
    {
        "jpeg" => ("image/jpeg", new JpegEncoder()),
        "bmp" => ("image/bmp", new BmpEncoder()),
        "png" => ("image/png", new PngEncoder()),
        _ => throw new NotSupportedException($"Format is not supported: {format}")
    };
}
