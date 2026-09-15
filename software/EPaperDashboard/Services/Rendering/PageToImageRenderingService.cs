using System.Diagnostics;
using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Guards;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Components.Playwright;

namespace EPaperDashboard.Services.Rendering;

internal sealed class PageToImageRenderingService(
    IHttpClientFactory httpClientFactory,
    IImageFactory imageFactory,
    PlaywrightComponentManager componentManager,
    ILogger<PageToImageRenderingService> logger) : IPageToImageRenderingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Health> GetHealth(Uri dashboardUri)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var dashboardHealth = await Result.Try(async () =>
        {
            var response = await httpClient.GetAsync(dashboardUri);
            return response.IsSuccessStatusCode;
        });

        dashboardHealth.TapError(error =>
            logger.LogError(error, "Dashboard health check failed for {DashboardUri}", dashboardUri));

        return new Health(true, dashboardHealth.GetValueOrDefault());
    }

    public Task<Result<IImage>> RenderDashboardAsync(
        Uri dashboardUri,
        Size size,
        HassTokens hassTokens) => Result.Try(async () =>
    {
        Guard.NotNull(dashboardUri);
        Guard.NotNull(hassTokens);

        var request = new ComponentRenderRequest(
            "dashboard",
            size.Width,
            size.Height,
            DashboardUri: dashboardUri.AbsoluteUri,
            AccessToken: hassTokens.AccessToken,
            TokenType: hassTokens.TokenType,
            HassUrl: hassTokens.HassUrl,
            ClientId: hassTokens.ClientId);

        var screenshot = await RunComponentAsync(request);
        logger.LogInformation("Rendered dashboard {DashboardUri} ({Size} bytes)", dashboardUri, screenshot.Length);
        return imageFactory.Load(screenshot);
    });

    public Task<Result<IImage>> RenderHtmlAsync(string html, Size size) => Result.Try(async () =>
    {
        Guard.NotNull(html);
        var screenshot = await RunComponentAsync(new ComponentRenderRequest(
            "html", size.Width, size.Height, Html: html));
        logger.LogInformation("Rendered SSR HTML ({Size} bytes)", screenshot.Length);
        return imageFactory.Load(screenshot);
    });

    private async Task<byte[]> RunComponentAsync(ComponentRenderRequest request)
    {
        var component = componentManager.GetActiveComponent()
            ?? throw new InvalidOperationException(
                "The Playwright component is not installed. An administrator can install it from System settings.");

        var outputPath = Path.Combine(Path.GetTempPath(), $"izboard-render-{Guid.NewGuid():N}.img");
        var requestWithOutput = request with { OutputPath = outputPath };

        try
        {
            using var process = new Process
            {
                StartInfo = CreateStartInfo(component),
                EnableRaisingEvents = true
            };
            if (!process.Start())
                throw new InvalidOperationException("The Playwright renderer could not be started.");

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(requestWithOutput, JsonOptions));
            process.StandardInput.Close();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("The Playwright renderer exceeded its 150 second timeout.");
            }

            var responseText = await standardOutput;
            var errorText = await standardError;
            var response = JsonSerializer.Deserialize<ComponentRenderResponse>(responseText, JsonOptions);
            if (process.ExitCode != 0 || response?.Success != true)
            {
                var message = response?.Error
                    ?? (string.IsNullOrWhiteSpace(errorText) ? "The Playwright renderer failed." : errorText.Trim());
                throw new InvalidOperationException(message);
            }
            if (!File.Exists(outputPath))
                throw new InvalidOperationException("The Playwright renderer did not produce an image.");

            return await File.ReadAllBytesAsync(outputPath);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ActivePlaywrightComponent component)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = component.RootPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(component.WorkerAssemblyPath);
        startInfo.Environment["PLAYWRIGHT_BROWSERS_PATH"] = component.BrowserPath;
        startInfo.Environment["IZBOARD_PLAYWRIGHT_LIBRARY_PATH"] = JoinEnvironmentPath(
            component.LibraryPath,
            Environment.GetEnvironmentVariable("LD_LIBRARY_PATH"));
        if (component.DataPath is not null)
        {
            startInfo.Environment["IZBOARD_PLAYWRIGHT_DATA_PATH"] = JoinEnvironmentPath(
                component.DataPath,
                Environment.GetEnvironmentVariable("XDG_DATA_DIRS"));
        }
        if (component.FontConfigPath is not null)
            startInfo.Environment["IZBOARD_PLAYWRIGHT_FONTCONFIG_PATH"] = component.FontConfigPath;
        return startInfo;
    }

    private static string JoinEnvironmentPath(string first, string? existing) =>
        string.IsNullOrWhiteSpace(existing) ? first : $"{first}{Path.PathSeparator}{existing}";

    private sealed record ComponentRenderRequest(
        string Mode,
        int Width,
        int Height,
        string? OutputPath = null,
        string? DashboardUri = null,
        string? Html = null,
        string? AccessToken = null,
        string? TokenType = null,
        string? HassUrl = null,
        string? ClientId = null);

    private sealed record ComponentRenderResponse(bool Success, string? Error);
}
