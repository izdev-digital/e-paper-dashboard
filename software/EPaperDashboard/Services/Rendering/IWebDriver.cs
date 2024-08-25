namespace EPaperDashboard.Services.Rendering;

public interface IWebDriver
{
    Task<byte[]> GetScreenshotAsync(Uri uri, Models.Rendering.Size size);
}
