using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using Microsoft.Playwright;

namespace EPaperDashboard.Services.Rendering;

public sealed class PageToImageRenderingService(
	IHttpClientFactory httpClientFactory,
	IImageFactory imageFactory,
	ILogger<PageToImageRenderingService> logger) : IPageToImageRenderingService
{
	private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
	private readonly IImageFactory _imageFactory = imageFactory;
	private readonly ILogger<PageToImageRenderingService> _logger = logger;

	public async Task<Health> GetHealth()
	{
		var httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10);

		var dashboardHealth = await Result.Try(async () =>
		{
			var dashboardResponse = await httpClient.GetAsync(EnvironmentConfiguration.DashboardUri);
			return dashboardResponse.IsSuccessStatusCode;
		});

		dashboardHealth.TapError(error => _logger.LogError(error));

		return new Health(true, dashboardHealth.GetValueOrDefault());
	}

	public Task<Result<IImage>> RenderDashboardAsync(Size size) => Result.Try(async () =>
	{
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync();
		var context = await browser.NewContextAsync(new BrowserNewContextOptions
		{
			ViewportSize = new ViewportSize
			{
				Width = size.Width,
				Height = size.Height
			}
		});

        var page = await context.NewPageAsync();
		var dashboardPage = new DashboardPage(page, EnvironmentConfiguration.DashboardUri);
		await dashboardPage.EnsureNavigatedAsync();
		await dashboardPage.SetToken(GetToken());
		await dashboardPage.EnsureNavigatedAsync();

		var screenshot = await dashboardPage.TakeScreenshotAsync();
        return _imageFactory.Load(screenshot);
	});

	private static HassTokens GetToken() => new(
		EnvironmentConfiguration.HassToken,
		"Bearer",
		EnvironmentConfiguration.HassUri.AbsoluteUri.TrimEnd('/'),
		EnvironmentConfiguration.ClientUri.AbsoluteUri.TrimEnd('/'));
}
