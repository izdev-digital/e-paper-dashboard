using System.Drawing;

namespace EPaperDashboard.Services.Rendering;

public interface IWebDriver
{
    Task<byte[]> GetScreenshotAsync(Uri uri, Size size);
}
