using EPaperDashboard.Models.Rendering;
using FluentResults;

namespace EPaperDashboard.Services.Rendering;

public interface IPageToImageRenderingService
{
    Task<Result<IImage>> RenderPageAsync(Size size);
}