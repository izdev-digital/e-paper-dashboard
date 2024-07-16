using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("[controller]")]
public class RenderToImageController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsImage([FromQuery] ImageSizeDto imageSize)
    {
        //"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --screenshot="/Users/izadorozhnyi/Desktop/temp.png" --ignore-certificate-errors --disable-proxy-certificate-handler --disable-content-security-policy --window-size=400,600 http://localhost:44447
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        TaskCompletionSource tcs = new();
        Process process = new();
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
            process.StartInfo.FileName = @"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
            process.StartInfo.Arguments = new StringBuilder()
                .Append("--headless=new --disable-gpu --hide-scrollbars")
                .AppendFormat(" --screenshot=\"{0}\"", tempPath)
                .Append(" --timeout=5000")
                .AppendFormat(" --window-size={0},{1}", imageSize.Width, imageSize.Height)
                .Append(" https://localhost:7297/Dashboard")
                .ToString();
            process.EnableRaisingEvents = true;
            process.Exited += (s, a) => tcs.TrySetResult();
            process.Start();
            await tcs.Task;
            var imageFileStream = System.IO.File.OpenRead(tempPath);
            return File(imageFileStream, "image/jpg");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
    }
}

public record ImageSizeDto(int Width, int Height);
