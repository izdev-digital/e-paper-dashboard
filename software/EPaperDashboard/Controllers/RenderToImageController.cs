using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using EPaperDashboard.Services.Rendering;
using SixLabors.ImageSharp.Processing;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/render")]
public class RenderToImageController(IPageToImageRenderingService renderingService) : ControllerBase
{
    //private readonly Uri _dashboardUri = new("https://th.bing.com/th/id/R.323e4192b13c8da0f81995c9569fcb3a?rik=LApkHct0Skqjtw&riu=http%3a%2f%2fimages4.fanpop.com%2fimage%2fphotos%2f17400000%2fBright-colored-world-bright-colors-17445519-1634-2560.jpg&ehk=7dpmHnNd8FihjJjg2LM%2fM8itdKJs2JQ3v62eVti2tA4%3d&risl=&pid=ImgRaw&r=0");
    private readonly Uri _dashboardUri = new("https://localhost:7297/dashboard");

    private readonly IPageToImageRenderingService _renderingService = renderingService;

    [HttpGet]
    [Route("text")]
    public async Task<IActionResult> GetAsText([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(
                    _dashboardUri,
                    new Services.Rendering.Size(imageSize.Width, imageSize.Height));
        if (imageResult.IsFailed)
        {
            return NoContent();
        }

        var palette = new[] { Color.Red, Color.Black, Color.White };
        var image = imageResult.Value
            .Quantize(palette)
            .RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal);

        var outStream = new MemoryStream();
        await image.SaveAsync(outStream, new BinaryEncoder(c => c == Color.Red.ToPixel<Rgba32>()));
        await image.SaveAsync(outStream, new BinaryEncoder(c => c == Color.Black.ToPixel<Rgba32>()));
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, "text/plain");
    }

    [HttpGet]
    [Route("converted")]
    public async Task<IActionResult> GetAsConvertedsImage([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(
            _dashboardUri,
            new Services.Rendering.Size(imageSize.Width, imageSize.Height));
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
    [Route("image")]
    public async Task<IActionResult> GetAsImage([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await _renderingService.RenderPageAsync(
            _dashboardUri,
            new Services.Rendering.Size(imageSize.Width, imageSize.Height));
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
