using FluentResults;

namespace EPaperDashboard.Services.Rendering;

public interface IPageToImageRenderingService
{
    Task<Result<IImage>> RenderPageAsync(Uri uri, Size size);
}