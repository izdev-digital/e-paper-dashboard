using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Guards;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/dashboards/{dashboardId}/images")]
[Authorize]
[DashboardOwner]
public class ImageUploadController(IEnvironmentConfiguration environmentConfiguration) : BaseApiController
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/bmp", "image/webp", "image/svg+xml"
    };

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    private string GetUploadsDir(string dashboardId) =>
        Path.Combine(environmentConfiguration.ConfigDir, "uploads", dashboardId);

    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> UploadImage(string dashboardId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided." });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { message = "File size exceeds the 10 MB limit." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}" });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = $"Content type '{file.ContentType}' is not allowed." });
        }

        var uploadsDir = GetUploadsDir(dashboardId);
        Directory.CreateDirectory(uploadsDir);

        // Generate a unique filename to avoid collisions
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var imageUrl = $"/api/dashboards/{dashboardId}/images/{fileName}";

        return Ok(new { imageUrl });
    }

    [HttpGet("{fileName}")]
    [AllowAnonymous]
    public IActionResult GetImage(string dashboardId, string fileName)
    {
        // Sanitize fileName to prevent directory traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        var filePath = Path.Combine(GetUploadsDir(dashboardId), fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Image not found." });
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }

    [HttpDelete("{fileName}")]
    public IActionResult DeleteImage(string dashboardId, string fileName)
    {
        // Sanitize fileName to prevent directory traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        var filePath = Path.Combine(GetUploadsDir(dashboardId), fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Image not found." });
        }

        System.IO.File.Delete(filePath);

        return Ok(new { message = "Image deleted successfully." });
    }

    [HttpPost("from-url")]
    public async Task<IActionResult> UploadImageFromUrl(string dashboardId, [FromBody] ImageFromUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
        {
            return BadRequest(new { message = "URL is required." });
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest(new { message = "Invalid URL. Please provide an HTTP or HTTPS URL." });
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "EPaperDashboard");

            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaxFileSize)
            {
                return BadRequest(new { message = "Remote file exceeds the 10 MB limit." });
            }

            // Determine extension from Content-Type or URL
            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            var extension = contentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => Path.GetExtension(uri.AbsolutePath)?.ToLowerInvariant()
            };

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = $"Unsupported image type '{contentType}'." });
            }

            var uploadsDir = GetUploadsDir(dashboardId);
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await response.Content.CopyToAsync(fileStream);

            // Verify we didn't exceed the limit (if Content-Length was absent)
            if (fileStream.Length > MaxFileSize)
            {
                fileStream.Close();
                System.IO.File.Delete(filePath);
                return BadRequest(new { message = "Remote file exceeds the 10 MB limit." });
            }

            var imageUrl = $"/api/dashboards/{dashboardId}/images/{fileName}";
            return Ok(new { imageUrl });
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { message = $"Failed to download image: {ex.Message}" });
        }
        catch (TaskCanceledException)
        {
            return BadRequest(new { message = "Download timed out." });
        }
    }
}

public record ImageFromUrlRequest(string Url);
