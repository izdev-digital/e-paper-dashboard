using FluentResults;
using SixLabors.ImageSharp.PixelFormats;

namespace EPaperDashboard.Services.Rendering;

public class PageToImageRenderingService(IWebDriver webDriver) : IPageToImageRenderingService
{
    private readonly IWebDriver _webDriver = webDriver;

    public async Task<Result<IImage>> RenderPageAsync(Uri uri, Size size)
    {
        try
        {
            var bytes = await _webDriver.GetScreenshotAsync(uri, size);
            return ImageAdapter<Rgba32>
                .Load(bytes)
                .Resize(size);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("Failed to render page to image").CausedBy(ex));
        }
    }
}
