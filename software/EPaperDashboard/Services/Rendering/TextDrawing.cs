using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

public enum HorizontalAlignment { Left, Center, Right }

public static class TextDrawing
{
    public static void DrawTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var measuredSize = TextMeasurer.MeasureSize(text, new TextOptions(font));

        if (measuredSize.Width <= bounds.Width)
        {
            var y = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(bounds.X, y)));
            return;
        }

        var ellipsis = "…";
        var ellipsisSize = TextMeasurer.MeasureSize(ellipsis, new TextOptions(font));
        var availableWidth = bounds.Width - ellipsisSize.Width;

        if (availableWidth <= 0)
        {
            var cy = bounds.Y + (bounds.Height - measuredSize.Height) / 2f;
            image.Mutate(ctx => ctx.DrawText(
                new RichTextOptions(font)
                {
                    Origin = new PointF(bounds.X, cy),
                    WrappingLength = bounds.Width,
                    WordBreaking = WordBreaking.BreakAll,
                },
                text, new SolidBrush(color), null));
            return;
        }

        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            var subSize = TextMeasurer.MeasureSize(text[..mid], new TextOptions(font));
            if (subSize.Width <= availableWidth)
                lo = mid;
            else
                hi = mid - 1;
        }

        var truncated = lo > 0 ? text[..lo] + ellipsis : ellipsis;
        var truncSize = TextMeasurer.MeasureSize(truncated, new TextOptions(font));
        var ty = bounds.Y + (bounds.Height - truncSize.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(truncated, font, color, new PointF(bounds.X, ty)));
    }

    public static float DrawWrappedTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, int maxLines = int.MaxValue, float lineSpacing = 0)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0 || maxLines <= 0)
            return 0;

        var glyphHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(font)).Height;
        var lineHeight = glyphHeight + lineSpacing;
        var ellipsis = "…";
        var ellipsisWidth = TextMeasurer.MeasureSize(ellipsis, new TextOptions(font)).Width;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 0;

        var lines = new List<string>();
        var currentLine = words[0];

        for (int i = 1; i < words.Length; i++)
        {
            var candidate = currentLine + " " + words[i];
            var candidateWidth = TextMeasurer.MeasureSize(candidate, new TextOptions(font)).Width;

            if (candidateWidth <= bounds.Width)
            {
                currentLine = candidate;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = words[i];
                if (lines.Count >= maxLines) break;
            }
        }

        if (lines.Count < maxLines)
            lines.Add(currentLine);

        var needsEllipsis = lines.Count > maxLines ||
            (lines.Count == maxLines && words.Length > 0 && !text.EndsWith(lines[^1]));

        if (lines.Count > maxLines)
        {
            lines = lines.Take(maxLines).ToList();
            needsEllipsis = true;
        }

        var reconstructed = string.Join(" ", lines);
        if (reconstructed.Length < text.Replace("  ", " ").Trim().Length)
            needsEllipsis = true;

        float yOffset = bounds.Y;
        for (int i = 0; i < lines.Count; i++)
        {
            if (yOffset + lineHeight > bounds.Bottom + 1) break;

            var line = lines[i];
            var isLastLine = i == lines.Count - 1;

            if (isLastLine && needsEllipsis)
            {
                var lineWidth = TextMeasurer.MeasureSize(line, new TextOptions(font)).Width;
                var widthAvailable = bounds.Width - ellipsisWidth;

                if (lineWidth > widthAvailable && widthAvailable > 0)
                {
                    int blo = 0, bhi = line.Length;
                    while (blo < bhi)
                    {
                        int mid = (blo + bhi + 1) / 2;
                        var subWidth = TextMeasurer.MeasureSize(line[..mid], new TextOptions(font)).Width;
                        if (subWidth <= widthAvailable) blo = mid; else bhi = mid - 1;
                    }
                    line = (blo > 0 ? line[..blo].TrimEnd() : "") + ellipsis;
                }
                else
                {
                    line = line.TrimEnd() + ellipsis;
                }
            }
            else if (isLastLine)
            {
                var lineWidth = TextMeasurer.MeasureSize(line, new TextOptions(font)).Width;
                if (lineWidth > bounds.Width)
                {
                    var widthAvailable = bounds.Width - ellipsisWidth;
                    int blo = 0, bhi = line.Length;
                    while (blo < bhi)
                    {
                        int mid = (blo + bhi + 1) / 2;
                        var subWidth = TextMeasurer.MeasureSize(line[..mid], new TextOptions(font)).Width;
                        if (subWidth <= widthAvailable) blo = mid; else bhi = mid - 1;
                    }
                    line = (blo > 0 ? line[..blo] : "") + ellipsis;
                }
            }

            image.Mutate(ctx => ctx.DrawText(line, font, color, new PointF(bounds.X, yOffset)));
            yOffset += lineHeight;
        }

        return yOffset - bounds.Y;
    }

    public static void DrawCenteredText(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    public static void DrawTextCentered(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));

        if (size.Width > bounds.Width)
        {
            DrawTextEllipsis(image, text, font, color, bounds);
            return;
        }

        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    public static void DrawTextAligned(IImageProcessingContext ctx, Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, HorizontalAlignment alignment)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = alignment switch
        {
            HorizontalAlignment.Right => bounds.Right - size.Width,
            HorizontalAlignment.Center => bounds.X + (bounds.Width - size.Width) / 2f,
            _ => bounds.X
        };
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        ctx.DrawText(text, font, color, new PointF(x, y));
    }
}
