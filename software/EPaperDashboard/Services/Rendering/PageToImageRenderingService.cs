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
		_logger.LogInformation("Checking health for dashboard at {DashboardUri}", dashboardUri);
		
		var httpClient = _httpClientFactory.CreateClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10);

		var dashboardHealth = await Result.Try(async () =>
		{
			var dashboardResponse = await httpClient.GetAsync(dashboardUri);
			_logger.LogInformation("Dashboard health check response: {StatusCode}", dashboardResponse.StatusCode);
			return dashboardResponse.IsSuccessStatusCode;
		});

		dashboardHealth.TapError(error => _logger.LogError(error, "Dashboard health check failed for {DashboardUri}", dashboardUri));

		var isHealthy = dashboardHealth.GetValueOrDefault();
		_logger.LogInformation("Dashboard health check result: {IsHealthy}", isHealthy);
		
		return new Health(true, isHealthy);
	}

	public Task<Result<IImage>> RenderDashboardAsync(Uri dashboardUri, Size size, IAuthrorizationStrategy authrorizationStrategy) => Result.Try(async () =>
	{
		_logger.LogInformation("Starting dashboard rendering for {DashboardUri} with size {Width}x{Height}", dashboardUri, size.Width, size.Height);
		
		Guard.NotNull(dashboardUri);
		Guard.NotNull(authrorizationStrategy);
		
		_logger.LogInformation("Creating Playwright instance");
		using var playwright = await Playwright.CreateAsync();
		
		_logger.LogInformation("Launching Chromium browser");
		await using var browser = await playwright.Chromium.LaunchAsync();
		
		_logger.LogInformation("Creating browser context with viewport {Width}x{Height}", size.Width, size.Height);
		var context = await browser.NewContextAsync(new BrowserNewContextOptions
		{
			ViewportSize = new ViewportSize
			{
				Width = size.Width,
				Height = size.Height
			}
		});

		_logger.LogInformation("Creating new page");
        var page = await context.NewPageAsync();
		var dashboardPage = new DashboardPage(page, dashboardUri);

		_logger.LogInformation("Authorizing page access");
		await authrorizationStrategy.AuthorizeAsync(dashboardPage);

		_logger.LogInformation("Navigating to dashboard");
		await dashboardPage.EnsureNavigatedAsync();
		
		_logger.LogInformation("Taking screenshot");
		var screenshot = await dashboardPage.TakeScreenshotAsync();
		
		_logger.LogInformation("Dashboard rendering completed successfully for {DashboardUri}, screenshot size: {Size} bytes", dashboardUri, screenshot.Length);
        return _imageFactory.Load(screenshot);
	});
}
