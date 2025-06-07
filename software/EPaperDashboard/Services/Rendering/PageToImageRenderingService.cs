using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using System.Text;

namespace EPaperDashboard.Services.Rendering;

public sealed class PageToImageRenderingService(IHttpClientFactory httpClientFactory, IImageFactory imageFactory, ILogger<PageToImageRenderingService> logger) : IPageToImageRenderingService
{
	private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
	private readonly IImageFactory _imageFactory = imageFactory;
	private readonly ILogger<PageToImageRenderingService> _logger = logger;

	public async Task<Health> GetHealth()
	{
		var httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10);
		var rendererHealth = await Result.Try(async () =>
		{
			var rendererHealthUri = new Uri(EnvironmentConfiguration.RendererUri, "health");
			var rendererResponse = await httpClient.GetAsync(rendererHealthUri);
			return rendererResponse.IsSuccessStatusCode;
		});

		var dashboardHealth = await Result.Try(async () =>
		{
			var dashboardResponse = await httpClient.GetAsync(EnvironmentConfiguration.DashboardUri);
			return dashboardResponse.IsSuccessStatusCode;
		});

		rendererHealth.TapError(error => _logger.LogError(error));
		dashboardHealth.TapError(error => _logger.LogError(error));

		return new Health(
			rendererHealth.GetValueOrDefault(), 
			dashboardHealth.GetValueOrDefault());
	}

	public async Task<Result<IImage>> RenderPageAsync(Size size)
	{
		var payload = $$"""
            {
                "url": "{{EnvironmentConfiguration.DashboardUri}}",
                "format": "jpeg",
                "window_width": {{size.Width}},
                "window_height": {{size.Height}},
                "pixel_density": 1
            }
            """;

		var httpClient = _httpClientFactory.CreateClient(Constants.RendererHttpClientName);
		var content = new StringContent(payload, Encoding.UTF8, "application/json");
		var response = await httpClient.PostAsync("capture", content);
		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			return Result.Failure<IImage>(error);
		}

		var bytes = await response.Content.ReadAsByteArrayAsync();
		var image = _imageFactory.Load(bytes);
		return Result.Success(image);
	}
}
