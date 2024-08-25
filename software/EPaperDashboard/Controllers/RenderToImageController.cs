using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using EPaperDashboard.Services.Rendering;
using SixLabors.ImageSharp.Processing;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/render")]
public class RenderToImageController(IPageToImageRenderingService renderingService) : ControllerBase
{
    private readonly Uri _dashboardUri = new("https://localhost:7297/dashboard");

    private readonly IPageToImageRenderingService _renderingService = renderingService;

    [HttpGet]
    [Route("binary")]
    public async Task<IActionResult> GetAsText([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(_dashboardUri, new Models.Rendering.Size(imageSize.Width, imageSize.Height));
        if (imageResult.IsFailed)
        {
            return NoContent();
        }

        var palette = new[] { Color.Red, Color.Black, Color.White };
        var image = imageResult.Value
            .Quantize(palette)
            .RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal);

        var outStream = new MemoryStream();
        await image.SaveAsync(outStream, new BlackRedWhiteBinaryEncoder());
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, "text/plain");
    }

    [HttpGet]
    [Route("converted")]
    public async Task<IActionResult> GetAsConvertedsImage([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(_dashboardUri, new Models.Rendering.Size(imageSize.Width, imageSize.Height));
        if (imageResult.IsFailed)
        {
            return NoContent();
        }

        var palette = new[] { Color.Red, Color.Black, Color.White };
        var image = imageResult.Value.Quantize(palette);
        var outStream = new MemoryStream();
        await image.SaveJpegAsync(outStream);
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, "image/jpg");
    }

    [HttpGet]
    [Route("original")]
    public async Task<IActionResult> GetAsImage([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(_dashboardUri, new Models.Rendering.Size(imageSize.Width, imageSize.Height));
        if (imageResult.IsFailed)
        {
            return NoContent();
        }

        var image = imageResult.Value;
        var outStream = new MemoryStream();
        await image.SaveJpegAsync(outStream);
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, "image/jpg");
    }
}

public record ImageSizeDto(int Width, int Height);
