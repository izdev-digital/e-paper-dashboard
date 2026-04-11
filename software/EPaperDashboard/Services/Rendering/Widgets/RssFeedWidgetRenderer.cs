using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using QRCoder;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class RssFeedWidgetRenderer(RenderingUtilities utils, ILogger<RssFeedWidgetRenderer> logger) : IWidgetRenderer
{
    public string WidgetType => "rss-feed";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var titleColor = ctx.TitleColor;
        var textColor = ctx.TextColor;
        var titleFontSize = ctx.TitleFontSize;
        var textFontSize = ctx.TextFontSize;
        var titleFontWeight = ctx.TitleFontWeight;
        var textFontWeight = ctx.TextFontWeight;
        var widgetBg = widget.ColorOverrides?.WidgetBackgroundColor ?? layout.ColorScheme.WidgetBackgroundColor;

        var entityId = RenderingUtilities.GetStringProp(widget.Config, "entityId") ?? "";
        var feedTitle = RenderingUtilities.GetStringProp(widget.Config, "title");

        if (string.IsNullOrEmpty(entityId)
            || !data.RssFeedEntries.TryGetValue(entityId, out var entries)
            || entries.Count == 0)
        {
            return Task.CompletedTask;
        }

        var entry = entries[0];
        float yOffset = contentRect.Y;

        if (widget.ShowTitle && !string.IsNullOrEmpty(widget.TitleOverride ?? feedTitle))
        {
            var feedTitleHeight = (int)Math.Ceiling(titleFontSize * 1.2f);
            var feedTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, feedTitleHeight);
            utils.DrawTextEllipsis(image, widget.TitleOverride ?? feedTitle!, utils.GetFont(titleFontSize, titleFontWeight), titleColor, feedTitleRect);
            yOffset += feedTitleHeight + 8;
        }

        var entryTitleFont = utils.GetFont(textFontSize, textFontWeight);
        var entryGlyphHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(entryTitleFont)).Height;
        var entryExtraSpacing = Math.Max(0, textFontSize * 1.3f - entryGlyphHeight);
        var entryLineHeight = entryGlyphHeight + entryExtraSpacing;
        var maxEntryLines = Math.Max(1, (int)((contentRect.Bottom - yOffset - 8) / entryLineHeight));
        maxEntryLines = Math.Min(maxEntryLines, 2);
        var entryTitleRect = new RectangleF(contentRect.X, yOffset, contentRect.Width, entryLineHeight * maxEntryLines);
        var entryTitleHeight = utils.DrawWrappedTextEllipsis(image, entry.Title, entryTitleFont, titleColor, entryTitleRect, maxEntryLines, entryExtraSpacing);
        yOffset += entryTitleHeight + 12;

        if (!string.IsNullOrEmpty(entry.Link))
        {
            try
            {
                var qrSize = Math.Min(contentRect.Width, contentRect.Bottom - yOffset);
                if (qrSize > 20)
                {
                    var darkColor = RenderingUtilities.ParseColor(layout.ColorScheme.Text);
                    var lightColor = RenderingUtilities.ParseColor(widgetBg);
                    var qrImage = GenerateQrCodeImage(entry.Link, darkColor, lightColor, (int)qrSize);
                    if (qrImage != null)
                    {
                        var qrX = (int)(contentRect.X + (contentRect.Width - qrSize) / 2);
                        var qrY = (int)yOffset;
                        image.Mutate(ctx => ctx.DrawImage(qrImage, new SixLabors.ImageSharp.Point(qrX, qrY), 1f));
                        qrImage.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to render QR code for RSS entry");
            }
        }

        return Task.CompletedTask;
    }

    private Image<Rgba32>? GenerateQrCodeImage(string url, Color darkColor, Color lightColor, int size)
    {
        try
        {
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var darkRgba = darkColor.ToPixel<Rgba32>();
            var lightRgba = lightColor.ToPixel<Rgba32>();
            var pngBytes = pngQrCode.GetGraphic(
                20,
                new byte[] { darkRgba.R, darkRgba.G, darkRgba.B, darkRgba.A },
                new byte[] { lightRgba.R, lightRgba.G, lightRgba.B, lightRgba.A });

            var qrImage = Image.Load<Rgba32>(pngBytes);
            qrImage.Mutate(ctx => ctx.Resize(new SixLabors.ImageSharp.Size(size, size)));
            return qrImage;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate QR code for URL: {Url}", url);
            return null;
        }
    }
}
