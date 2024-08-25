using EPaperDashboard.Models.Rendering;
using OpenQA.Selenium.Chrome;

namespace EPaperDashboard.Services.Rendering;

public sealed class WebDriver : IWebDriver, IDisposable
{
    private readonly ChromeDriver _driver;
    private readonly SemaphoreSlim _lock = new(1, 1);
    public WebDriver()
    {
        var options = new ChromeOptions();
        options.AddArguments(
            "--headless=new",
            "--disable-gpu",
            "--hide-scrollbars");

        _driver = new ChromeDriver(options);
    }

    public async Task<byte[]> GetScreenshotAsync(Uri uri, Size size)
    {
        await _lock.WaitAsync();
        try
        {
            _driver.Manage().Window.Size = new System.Drawing.Size(size.Width, size.Height);
            await _driver.Navigate().GoToUrlAsync(uri);
            return _driver.GetScreenshot().AsByteArray;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _driver.Close();
}