using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
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
    DashboardImageRenderingService dashboardImageRenderingService) : BaseApiController
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
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var layoutConfigJson = System.Text.Json.JsonSerializer.Serialize(dashboard.Value.LayoutConfig, serializerOptions);

            var rawImage = await dashboardImageRenderingService.RenderDashboardImageAsync(
                dashboard.Value.Id.ToString(),
                layoutConfigJson);

            IImage image = ImageAdapter<SixLabors.ImageSharp.PixelFormats.Rgba32>.Wrap(rawImage);

            var (contentType, encoder) = GetEncoder(format);
            return await ConvertToResult(image, encoder, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to render dashboard image: {ex.Message}");
        }
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
