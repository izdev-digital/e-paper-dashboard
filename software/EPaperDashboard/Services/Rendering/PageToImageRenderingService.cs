using EPaperDashboard.Models.Rendering;
using FluentResults;

namespace EPaperDashboard.Services.Rendering;

public class PageToImageRenderingService(
    IWebDriver webDriver,
    IImageFactory imageFactory) : IPageToImageRenderingService
{
    private readonly IWebDriver _webDriver = webDriver;
    private readonly IImageFactory _imageFactory = imageFactory;

    public async Task<Result<IImage>> RenderPageAsync(Uri uri, Size size)
    {
        try
        {
            var bytes = await _webDriver.GetScreenshotAsync(uri, size);
            var image = _imageFactory.Load(bytes).Resize(size);
            return Result.Ok(image);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("Failed to render page to image").CausedBy(ex));
        }
    }
}
