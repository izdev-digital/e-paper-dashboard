using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Rendering;

public interface IPageToImageRenderingService
{
	Task<Health> GetHealth(Uri dashboardUri);
	Task<Result<IImage>> RenderDashboardAsync(Uri dashboardUri, Size size, IAuthrorizationStrategy authrorizationStrategy);
}
