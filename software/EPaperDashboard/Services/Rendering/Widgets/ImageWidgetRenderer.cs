using System.Buffers;
using System.Text.RegularExpressions;
using EPaperDashboard.Models.Rendering;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Color = SixLabors.ImageSharp.Color;
using RectangleF = SixLabors.ImageSharp.RectangleF;
using Size = SixLabors.ImageSharp.Size;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed partial class ImageWidgetRenderer(
    RenderingUtilities utils,
    IHttpClientFactory httpClientFactory,
    Utilities.IEnvironmentConfiguration environmentConfiguration,
    ILogger<ImageWidgetRenderer> logger) : IWidgetRenderer
{
    public string WidgetType => "image";

    [GeneratedRegex(@"^/api/dashboards/([^/]+)/images/([^/]+)$")]
    private static partial Regex LocalImagePathRegex();

    public async Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        var renderContext = WidgetRenderContext.Create(widget, layout);
        contentRect = WidgetFrameRenderer.DrawOptionalCenteredTitle(image, widget, layout, utils, contentRect);

        if (contentRect.Width <= 0 || contentRect.Height <= 0)
            return;

        var imageUrl = RenderingUtilities.GetStringProp(widget.Config, "imageUrl") ?? "";
        if (string.IsNullOrEmpty(imageUrl)) return;

        try
        {
            byte[] imageBytes;

            // Images are stored on disk and served via /api/dashboards/{id}/images/{file}
            // Load directly from disk instead of making an HTTP request to ourselves.
            var localMatch = LocalImagePathRegex().Match(imageUrl);
            if (localMatch.Success)
            {
                var dashId = localMatch.Groups[1].Value;
                var fileName = localMatch.Groups[2].Value;
                // Guard against traversal — reject suspicious characters then verify canonical path
                if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
                    return;
                var uploadsDir = Path.GetFullPath(Path.Combine(
                    environmentConfiguration.ConfigDir, "uploads", dashId));
                var filePath = Path.GetFullPath(Path.Combine(uploadsDir, fileName));
                if (!filePath.StartsWith(uploadsDir + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && filePath != uploadsDir)
                    return;
                if (!File.Exists(filePath))
                {
                    logger.LogWarning("Image file not found on disk: {Path}", filePath);
                    return;
                }
                imageBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            }
            else
            {
                // External URL — use named client with pre-configured timeout
                using var httpClient = httpClientFactory.CreateClient(Utilities.Constants.SsrImageHttpClientName);
                using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Reject downloads exceeding 10 MB to prevent memory exhaustion
                const long maxBytes = 10 * 1024 * 1024;
                if (response.Content.Headers.ContentLength > maxBytes)
                {
                    logger.LogWarning("Image download rejected — Content-Length {Length} exceeds limit", response.Content.Headers.ContentLength);
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var ms = new MemoryStream();
                var buffer = ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    long totalRead = 0;
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        totalRead += bytesRead;
                        if (totalRead > maxBytes)
                        {
                            logger.LogWarning("Image download aborted — exceeded {Limit} bytes", maxBytes);
                            return;
                        }
                        ms.Write(buffer, 0, bytesRead);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                imageBytes = ms.ToArray();
            }

            using var srcImage = Image.Load<Rgba32>(imageBytes);

            var zoom = RenderingUtilities.GetDoubleProp(widget.Config, "zoom") ?? 1.0;
            var panX = RenderingUtilities.GetDoubleProp(widget.Config, "offsetX") ?? 0.0;
            var panY = RenderingUtilities.GetDoubleProp(widget.Config, "offsetY") ?? 0.0;

            var containerW = contentRect.Width;
            var containerH = contentRect.Height;

            // The Angular component sets the img element to (zoom * 100%) of the container,
            // then uses object-fit: contain to preserve aspect ratio while keeping the
            // entire image within the element so panning can reach all edges.
            var imgElW = containerW * (float)zoom;
            var imgElH = containerH * (float)zoom;

            // Fit the source image within the virtual img element (object-fit: contain)
            float srcAspect = (float)srcImage.Width / srcImage.Height;
            float elAspect = imgElW / imgElH;

            float drawW, drawH;
            if (srcAspect > elAspect)
            {
                drawW = imgElW;
                drawH = imgElW / srcAspect;
            }
            else
            {
                drawH = imgElH;
                drawW = imgElH * srcAspect;
            }

            // Center the fitted image within the virtual element
            float fitOffsetX = (imgElW - drawW) / 2f;
            float fitOffsetY = (imgElH - drawH) / 2f;

            // Angular positions the img element at:
            //   left = -((zoom - 1) * (offsetX + 1) * 50)%   of container width
            //   top  = -((zoom - 1) * (offsetY + 1) * 50)%   of container height
            float elLeft = -(float)((zoom - 1) * (panX + 1) * 50.0 / 100.0) * containerW;
            float elTop = -(float)((zoom - 1) * (panY + 1) * 50.0 / 100.0) * containerH;

            // Final draw position = container origin + element offset + fit centering
            float drawX = contentRect.X + elLeft + fitOffsetX;
            float drawY = contentRect.Y + elTop + fitOffsetY;

            // Resize source image to the fitted dimensions
            var resizedW = Math.Max(1, (int)Math.Round(drawW));
            var resizedH = Math.Max(1, (int)Math.Round(drawH));
            srcImage.Mutate(ctx => ctx.Resize(new Size(resizedW, resizedH)));

            // Apply per-widget dithering to the source image before compositing
            var dithering = RenderingUtilities.GetBoolProp(widget.Config, "dithering") ?? false;
            {
                var paletteColors = layout.ColorScheme.Palette
                    .Select(hex => ColorUtils.ParseColor(hex))
                    .ToArray();
                if (paletteColors.Length > 0)
                {
                    srcImage.Mutate(ctx => ctx.Quantize(new PaletteQuantizer(
                        new ReadOnlyMemory<Color>(paletteColors),
                        new QuantizerOptions { Dither = dithering ? KnownDitherings.JarvisJudiceNinke : null })));
                }
            }

            // Clip the source image to the visible portion within the content rect
            // (the Angular container has overflow: hidden)
            int srcDrawX = (int)Math.Round(drawX);
            int srcDrawY = (int)Math.Round(drawY);

            int clipLeft = Math.Max(0, (int)contentRect.X - srcDrawX);
            int clipTop = Math.Max(0, (int)contentRect.Y - srcDrawY);
            int clipRight = Math.Min(srcImage.Width, (int)(contentRect.X + contentRect.Width) - srcDrawX);
            int clipBottom = Math.Min(srcImage.Height, (int)(contentRect.Y + contentRect.Height) - srcDrawY);

            int clipW = clipRight - clipLeft;
            int clipH = clipBottom - clipTop;

            if (clipW > 0 && clipH > 0)
            {
                using var clipped = srcImage.Clone(ctx =>
                    ctx.Crop(new SixLabors.ImageSharp.Rectangle(clipLeft, clipTop, clipW, clipH)));

                image.Mutate(ctx => ctx.DrawImage(clipped,
                    new SixLabors.ImageSharp.Point(srcDrawX + clipLeft, srcDrawY + clipTop), 1f));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load image from URL: {Url}", imageUrl);
            TextDrawing.DrawCenteredText(image, "Image", utils.GetFont(renderContext.TextFontSize), renderContext.TextColor, contentRect);
        }
    }
}
