using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using FluentResults;
using System.Text;

namespace EPaperDashboard.Services.Rendering;

public sealed class PageToImageRenderingService(
	IHttpClientFactory httpClientFactory,
	IImageFactory imageFactory) : IPageToImageRenderingService
{
	private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
	private readonly IImageFactory _imageFactory = imageFactory;

	public async Task<Result<IImage>> RenderPageAsync(Size size)
	{
		var payload = $$"""
            {
                "url": "{{EnvironmentConfiguration.DashboardUri.Value}}",
                "format": "jpeg",
                "window_width": {{size.Width}},
                "window_height": {{size.Height}},
                "pixel_density": 1
            }
            """;

		var httpClient = _httpClientFactory.CreateClient(Constants.RendererHttpClientName);
		var content = new StringContent(payload, Encoding.UTF8, "application/json");
		var response = await httpClient.PostAsync("capture", content);
		if(!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			return new Error(error);
		}

		var bytes = await response.Content.ReadAsByteArrayAsync();
		var image = _imageFactory.Load(bytes);
		return Result.Ok(image);
	}
}
