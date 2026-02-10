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
		await using var browser = await playwright.Chromium.LaunchAsync(GetLaunchOptions());
		
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

	public Task<Result<IImage>> RenderHtmlAsync(string html, Size size) => Result.Try(async () =>
	{
		Guard.NotNull(html);

		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync(GetLaunchOptions());

		var context = await browser.NewContextAsync(new BrowserNewContextOptions
		{
			ViewportSize = new ViewportSize { Width = size.Width, Height = size.Height }
		});

		var page = await context.NewPageAsync();
		await page.SetContentAsync(html, new PageSetContentOptions
		{
			WaitUntil = WaitUntilState.NetworkIdle,
			Timeout = 10000
		});

		var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });

		_logger.LogInformation("Rendered SSR HTML ({Size} bytes)", screenshot.Length);

		return _imageFactory.Load(screenshot);
	});

	/// <summary>
	/// Gets browser launch options with appropriate security settings.
	/// When running as root (e.g., in Home Assistant addon), disables sandbox.
	/// </summary>
	private static BrowserTypeLaunchOptions GetLaunchOptions()
	{
		var options = new BrowserTypeLaunchOptions();
		
		// Chromium doesn't allow running as root without --no-sandbox
		// This is safe in containerized environments like HA addons
		if (Environment.UserName == "root" || Environment.GetEnvironmentVariable("USER") == "root")
		{
			options.Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" };
		}
		
		return options;
	}
}
