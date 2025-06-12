using CSharpFunctionalExtensions;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using Microsoft.Playwright;
using Newtonsoft.Json;

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

	public async Task<Result<IImage>> RenderDashboardAsync(Size size)
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

		var token = new HassTokens
		{
			AccessToken = EnvironmentConfiguration.HassToken,
			ClientId = EnvironmentConfiguration.ClientUri.AbsoluteUri.TrimEnd('/'),
			HassUrl = EnvironmentConfiguration.HassUri.AbsoluteUri.TrimEnd('/'),
			TokenType = "Bearer"
		};
		var hassTokens = JsonConvert.SerializeObject(token);
		var page = await context.NewPageAsync();
		await page.GotoAsync(EnvironmentConfiguration.DashboardUri.AbsoluteUri);
		await page.WaitForLoadStateAsync(LoadState.Load);
		await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
		await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		await page.EvaluateAsync("token => { localStorage.setItem('hassTokens', token); }", hassTokens);
		var feedback = await page.EvaluateAsync<string>("() => { return localStorage.getItem('hassTokens'); }");
		_logger.LogInformation("Hass tokens from page: {0}", feedback);
		await page.GotoAsync(EnvironmentConfiguration.DashboardUri.AbsoluteUri);
		await page.WaitForLoadStateAsync(LoadState.Load);
		await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
		await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
		var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Jpeg });
		var image = _imageFactory.Load(screenshot);
		return Result.Success(image);
	}
}
