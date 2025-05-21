using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Rendering;

public interface IPageToImageRenderingService
{
    Task<Result<IImage>> RenderPageAsync(Size size);
}