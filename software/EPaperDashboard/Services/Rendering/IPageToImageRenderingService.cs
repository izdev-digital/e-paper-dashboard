using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Rendering;

public interface IPageToImageRenderingService
{
	Task<Health> GetHealth();
	Task<Result<IImage>> RenderPageAsync(Size size);
}

public readonly record struct Health(bool IsRendererAvailable, bool IsDashboardAvailable);