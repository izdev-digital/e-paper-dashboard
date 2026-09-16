using CSharpFunctionalExtensions;
using EPaperDashboard.Guards;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.RenderingComponent;
using EPaperDashboard.Services.Components.Playwright;

namespace EPaperDashboard.Services.Rendering;

internal sealed class PageToImageRenderingService(
    IHttpClientFactory httpClientFactory,
    IImageFactory imageFactory,
    PlaywrightComponentManager componentManager,
    PlaywrightComponentRuntime runtime,
    ILogger<PageToImageRenderingService> logger) : IPageToImageRenderingService
{
    public async Task<Health> GetHealth(Uri dashboardUri)
    {
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var dashboardHealth = await Result.Try(async () =>
        {
            using var response = await httpClient.GetAsync(dashboardUri);
            return response.IsSuccessStatusCode;
        });
        dashboardHealth.TapError(error =>
            logger.LogError(error, "Dashboard health check failed for {DashboardUri}", dashboardUri));
        return new Health(componentManager.GetActiveComponent() is not null && await runtime.IsAvailableAsync(), dashboardHealth.GetValueOrDefault());
    }

    public Task<Result<IImage>> RenderDashboardAsync(
        Uri dashboardUri, Size size, HassTokens hassTokens, CancellationToken cancellationToken = default) => Result.Try(async () =>
    {
        Guard.NotNull(dashboardUri);
        Guard.NotNull(hassTokens);
        return await RenderAsync(new RenderRequest(
            "dashboard", size.Width, size.Height,
            DashboardUri: dashboardUri.AbsoluteUri, AccessToken: hassTokens.AccessToken,
            TokenType: hassTokens.TokenType, HassUrl: hassTokens.HassUrl, ClientId: hassTokens.ClientId), cancellationToken);
    });

    public Task<Result<IImage>> RenderHtmlAsync(
        string html, Size size, CancellationToken cancellationToken = default) => Result.Try(async () =>
    {
        Guard.NotNull(html);
        return await RenderAsync(new RenderRequest("html", size.Width, size.Height, Html: html), cancellationToken);
    });

    private async Task<IImage> RenderAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        request.Validate();
        using var queueTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        queueTimeout.CancelAfter(TimeSpan.FromSeconds(150));
        using var lease = await runtime.AcquireRenderAsync(queueTimeout.Token);
        var component = componentManager.GetActiveComponent()
            ?? throw new InvalidOperationException(
                "The rendering component is not ready. An administrator can manage it from System settings.");
        var screenshot = await runtime.RunAsync(component, request, cancellationToken);
        return imageFactory.Load(screenshot, new Size(request.Width, request.Height));
    }
}
