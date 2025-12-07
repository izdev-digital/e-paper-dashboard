using CSharpFunctionalExtensions;
using EPaperDashboard.Guards;
using EPaperDashboard.Models.Rendering;
using Microsoft.Playwright;

namespace EPaperDashboard.Services.Rendering;

internal sealed class PageToImageRenderingService(
	IHttpClientFactory httpClientFactory,
	IImageFactory imageFactory,
	ILogger<PageToImageRenderingService> logger) : IPageToImageRenderingService
{
	private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
	private readonly IImageFactory _imageFactory = imageFactory;
	private readonly ILogger<PageToImageRenderingService> _logger = logger;

	public async Task<Health> GetHealth(Uri dashboardUri)
	{
		var httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10);

		var dashboardHealth = await Result.Try(async () =>
		{
			var response = await httpClient.GetAsync(dashboardUri);
			return response.IsSuccessStatusCode;
		});

		dashboardHealth.TapError(error => 
			_logger.LogError(error, "Dashboard health check failed for {DashboardUri}", dashboardUri));

		return new Health(true, dashboardHealth.GetValueOrDefault());
	}

	public Task<Result<IImage>> RenderDashboardAsync(
		Uri dashboardUri, 
		Size size, 
		IAuthrorizationStrategy authrorizationStrategy) => Result.Try(async () =>
	{
		Guard.NotNull(dashboardUri);
		Guard.NotNull(authrorizationStrategy);
		
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync();
		
		var context = await browser.NewContextAsync(new BrowserNewContextOptions
		{
			ViewportSize = new ViewportSize { Width = size.Width, Height = size.Height }
		});

		var page = await context.NewPageAsync();
		var dashboardPage = new DashboardPage(page, dashboardUri);

		await authrorizationStrategy.AuthorizeAsync(dashboardPage);
		await dashboardPage.EnsureNavigatedAsync();
		
		var screenshot = await dashboardPage.TakeScreenshotAsync();
		
		_logger.LogInformation("Rendered dashboard {DashboardUri} ({Size} bytes)", 
			dashboardUri, screenshot.Length);
		
		return _imageFactory.Load(screenshot);
	});
}
