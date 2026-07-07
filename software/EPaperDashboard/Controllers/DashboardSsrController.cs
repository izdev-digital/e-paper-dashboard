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
    IEnvironmentConfiguration environmentConfiguration) : BaseApiController
{
    /// <summary>
    /// Returns the dashboard rendered directly to an image using ImageSharp.
    /// </summary>
    [HttpGet("{id}/render-image")]
    public async Task<IActionResult> RenderDashboardImage(string id, [FromQuery] string format = "jpeg")
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

    private static (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
    {
        "jpeg" => ("image/jpeg", new JpegEncoder()),
        "bmp" => ("image/bmp", new BmpEncoder()),
        "png" => ("image/png", new PngEncoder()),
        _ => throw new NotSupportedException($"Format is not supported: {format}")
    };
}
