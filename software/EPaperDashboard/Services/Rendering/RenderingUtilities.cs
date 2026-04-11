using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Utilities;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Shared drawing utilities used by widget renderers and the main rendering service.
/// </summary>
public sealed class RenderingUtilities
{
    private readonly FontFamily _fontFamily;
    private readonly FontAwesomeIconRegistry _iconRegistry;
    private readonly Dictionary<(int Size, FontStyle Style), Font> _fontCache = new();

    public RenderingUtilities(FontFamily fontFamily, FontAwesomeIconRegistry iconRegistry)
    {
        _fontFamily = fontFamily;
        _iconRegistry = iconRegistry;
    }

    /// <summary>
    /// Creates a <see cref="RenderingUtilities"/> instance using the web root fonts
    /// and the given icon registry. Used for DI factory registration.
    /// </summary>
    public static RenderingUtilities Create(IWebHostEnvironment env, FontAwesomeIconRegistry iconRegistry)
    {
        var fontFamily = LoadFontFamily(env.WebRootPath);
        return new RenderingUtilities(fontFamily, iconRegistry);
    }

    private static FontFamily LoadFontFamily(string? webRootPath)
    {
        if (!string.IsNullOrEmpty(webRootPath))
        {
            var fontsDir = System.IO.Path.Combine(webRootPath, "fonts");
            if (Directory.Exists(fontsDir))
            {
                var collection = new FontCollection();
                foreach (var file in Directory.GetFiles(fontsDir, "*.ttf")
                    .Concat(Directory.GetFiles(fontsDir, "*.otf")))
                {
                    try { return collection.Add(file); }
                    catch { /* skip unreadable font files */ }
                }
            }
        }

        if (SystemFonts.TryGet("Inter", out var systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("Roboto", out systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("DejaVu Sans", out systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("Liberation Sans", out systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("Arial", out systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("Helvetica", out systemFamily)) return systemFamily;
        if (SystemFonts.TryGet("Segoe UI", out systemFamily)) return systemFamily;

        foreach (var family in SystemFonts.Families)
            return family;

        throw new InvalidOperationException("No fonts available on the system for rendering.");
    }

    // =============================================
    // FONT HELPERS
    // =============================================

    public Font GetFont(int size, FontStyle style = FontStyle.Regular)
    {
        var key = (size, style);
        if (!_fontCache.TryGetValue(key, out var font))
        {
            font = _fontFamily.CreateFont(size, style);
            _fontCache[key] = font;
        }
        return font;
    }

    public Font GetFont(int size, int weight)
    {
        var style = weight >= 700 ? FontStyle.Bold : FontStyle.Regular;
        return GetFont(size, style);
    }

    // =============================================
    // COLOR HELPERS
    // =============================================

    public static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Color.Black;
        try { return Color.ParseHex(hex); }
        catch { return Color.Black; }
    }

    public static Color WithOpacity(Color color, float opacity)
    {
        var p = color.ToPixel<Rgba32>();
        return new Color(new Rgba32(p.R, p.G, p.B, (byte)(p.A * Math.Clamp(opacity, 0f, 1f))));
    }

    public static Color ResolveWidgetColor(
        WidgetConfigEntry widget,
        LayoutConfig layout,
        Func<ColorSchemeConfig, string> schemeSelector,
        Func<WidgetColorOverridesConfig?, string?> overrideSelector)
    {
        var hex = overrideSelector(widget.ColorOverrides) ?? schemeSelector(layout.ColorScheme);
        return ParseColor(hex);
    }

    // =============================================
    // JSON PROPERTY HELPERS
    // =============================================

    public static string? GetStringProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    public static int? GetIntProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

    public static double? GetDoubleProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    public static bool? GetBoolProp(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
            ? p.ValueKind == JsonValueKind.True ? true : p.ValueKind == JsonValueKind.False ? false : null
            : null;

    public static string[]? GetStringArrayProp(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p) || p.ValueKind != JsonValueKind.Array)
            return null;
        return p.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .ToArray();
    }

    public static double? GetBadgeDoubleProp(JsonElement badge, string prop) =>
        badge.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;

    public static string? GetEntityAttr(HassEntityState state, string key)
    {
        if (state.Attributes.TryGetValue(key, out var val) && val != null)
        {
            return val switch
            {
                string s => s,
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                _ => val.ToString()
            };
        }
        return null;
    }

    // =============================================
    // TEXT DRAWING HELPERS
    // =============================================

    public void DrawTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
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

    public float DrawWrappedTextEllipsis(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds, int maxLines = int.MaxValue, float lineSpacing = 0)
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

    public void DrawCenteredText(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(text)) return;
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var x = bounds.X + (bounds.Width - size.Width) / 2f;
        var y = bounds.Y + (bounds.Height - size.Height) / 2f;
        image.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(x, y)));
    }

    public void DrawTextCentered(Image<Rgba32> image, string text, Font font, Color color, RectangleF bounds)
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

    // =============================================
    // INLINE ICON TEXT DRAWING
    // =============================================

    private static readonly Regex InlineIconPattern = new(@":fa-([a-z0-9-]+):", RegexOptions.Compiled);

    public float DrawTextWithInlineIcons(Image<Rgba32> image, string text, Font font,
        Color textColor, Color iconColor, RectangleF bounds, int maxLines = int.MaxValue, float lineSpacing = 0)
    {
        if (!text.Contains(":fa-"))
            return DrawWrappedTextEllipsis(image, text, font, textColor, bounds, maxLines, lineSpacing);

        var segments = ParseIconSegments(text);
        if (segments.Count == 0) return 0;

        var glyphHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(font)).Height;
        var lineHeight = glyphHeight + lineSpacing;
        var iconSize = glyphHeight;
        var spaceWidth = TextMeasurer.MeasureSize(" ", new TextOptions(font)).Width;

        float x = bounds.X;
        float y = bounds.Y;
        int lineCount = 1;

        for (int si = 0; si < segments.Count; si++)
        {
            var seg = segments[si];
            if (y + lineHeight > bounds.Bottom + 1 || lineCount > maxLines) break;

            if (seg.IsIcon)
            {
                if (x > bounds.X && seg.LeadingSpace)
                    x += spaceWidth;

                if (x + iconSize > bounds.Right && x > bounds.X)
                {
                    x = bounds.X;
                    y += lineHeight;
                    lineCount++;
                    if (lineCount > maxLines || y + lineHeight > bounds.Bottom + 1) break;
                }
                DrawFaIcon(image, seg.Text, iconColor, new RectangleF(x, y + 1, iconSize, iconSize));
                x += iconSize;

                if (seg.TrailingSpace)
                    x += spaceWidth;
            }
            else
            {
                var words = seg.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool firstWord = true;
                foreach (var word in words)
                {
                    bool needsSpace = x > bounds.X && (!firstWord || seg.LeadingSpace);
                    var wordStr = needsSpace ? " " + word : word;
                    var wordWidth = TextMeasurer.MeasureSize(wordStr, new TextOptions(font)).Width;

                    if (x + wordWidth > bounds.Right && x > bounds.X)
                    {
                        x = bounds.X;
                        y += lineHeight;
                        lineCount++;
                        if (lineCount > maxLines || y + lineHeight > bounds.Bottom + 1) break;
                        wordStr = word;
                        wordWidth = TextMeasurer.MeasureSize(wordStr, new TextOptions(font)).Width;
                    }

                    image.Mutate(ctx => ctx.DrawText(wordStr, font, textColor, new PointF(x, y)));
                    x += wordWidth;
                    firstWord = false;
                }
                if (lineCount > maxLines) break;
            }
        }

        return y - bounds.Y + lineHeight;
    }

    private static List<(string Text, bool IsIcon, bool LeadingSpace, bool TrailingSpace)> ParseIconSegments(string text)
    {
        var segments = new List<(string Text, bool IsIcon, bool LeadingSpace, bool TrailingSpace)>();
        int lastEnd = 0;

        foreach (Match m in InlineIconPattern.Matches(text))
        {
            if (m.Index > lastEnd)
            {
                var run = text[lastEnd..m.Index];
                segments.Add((run, false, false, false));
            }

            bool leadingSpace = m.Index > 0 && char.IsWhiteSpace(text[m.Index - 1]);
            bool trailingSpace = m.Index + m.Length < text.Length && char.IsWhiteSpace(text[m.Index + m.Length]);
            segments.Add(($"fa-{m.Groups[1].Value}", true, leadingSpace, trailingSpace));
            lastEnd = m.Index + m.Length;
        }

        if (lastEnd < text.Length)
            segments.Add((text[lastEnd..], false, false, false));

        return segments;
    }

    // =============================================
    // FA ICON DRAWING
    // =============================================

    public void DrawFaIcon(Image<Rgba32> image, string? iconClass, Color color, RectangleF bounds)
    {
        if (string.IsNullOrEmpty(iconClass) || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        if (!_iconRegistry.TryGetIcon(iconClass, out var entry))
            return;

        try
        {
            var path = SvgPathParser.Parse(entry.Path);
            var pathBounds = path.Bounds;
            if (pathBounds.Width < 0.1f || pathBounds.Height < 0.1f)
                return;

            var scale = Math.Min(bounds.Width / entry.VbW, bounds.Height / entry.VbH);
            var offsetX = bounds.X + (bounds.Width - entry.VbW * scale) / 2f;
            var offsetY = bounds.Y + (bounds.Height - entry.VbH * scale) / 2f;

            var matrix = Matrix3x2.CreateScale(scale) *
                         Matrix3x2.CreateTranslation(offsetX, offsetY);

            var transformed = path.Transform(matrix);
            image.Mutate(ctx => ctx.Fill(color, transformed));
        }
        catch
        {
            // Silently ignore icon rendering failures
        }
    }

    // =============================================
    // APP ICON DRAWING
    // =============================================

    public static void DrawAppIcon(Image<Rgba32> image, Color accentColor, RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        const float vb = 370f;
        var scale = Math.Min(bounds.Width / vb, bounds.Height / vb);
        var ox = bounds.X + (bounds.Width - vb * scale) / 2f;
        var oy = bounds.Y + (bounds.Height - vb * scale) / 2f;

        var p = accentColor.ToPixel<Rgba32>();
        var darkest  = new Color(new Rgba32((byte)(p.R * 0.3f), (byte)(p.G * 0.3f), (byte)(p.B * 0.3f), p.A));
        var darker   = new Color(new Rgba32((byte)(p.R * 0.7f), (byte)(p.G * 0.7f), (byte)(p.B * 0.7f), p.A));
        var baseC    = accentColor;
        var light    = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.2f), (byte)(p.G + (255 - p.G) * 0.2f), (byte)(p.B + (255 - p.B) * 0.2f), p.A));
        var lighter  = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.4f), (byte)(p.G + (255 - p.G) * 0.4f), (byte)(p.B + (255 - p.B) * 0.4f), p.A));
        var lightest = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.6f), (byte)(p.G + (255 - p.G) * 0.6f), (byte)(p.B + (255 - p.B) * 0.6f), p.A));

        (float x, float y, float w, float h, float rx, Color c)[] rects =
        [
            (20, 20, 90, 96, 4, darkest),
            (20, 128, 90, 196, 4, darker),
            (122, 20, 134, 96, 4, baseC),
            (268, 20, 82, 96, 4, light),
            (122, 236, 84, 88, 4, light),
            (218, 236, 132, 88, 4, lighter),
        ];

        foreach (var (rx, ry, rw, rh, rrx, color) in rects)
        {
            var x1 = rx * scale + ox;
            var y1 = ry * scale + oy;
            var w1 = rw * scale;
            var h1 = rh * scale;
            var cr = Math.Min(rrx * scale, Math.Min(w1, h1) / 2f);
            image.Mutate(ctx => ctx.Fill(color, BuildRoundedRect(x1, y1, w1, h1, cr)));
        }

        PointF[] leftPoly = [
            new(122 * scale + ox, 128 * scale + oy),
            new(256 * scale + ox, 128 * scale + oy),
            new(206 * scale + ox, 224 * scale + oy),
            new(122 * scale + ox, 224 * scale + oy),
        ];
        PointF[] rightPoly = [
            new(268 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 128 * scale + oy),
            new(350 * scale + ox, 224 * scale + oy),
            new(218 * scale + ox, 224 * scale + oy),
        ];

        image.Mutate(ctx =>
        {
            ctx.Fill(lightest, new Polygon(new LinearLineSegment(leftPoly)));
            ctx.Fill(lighter, new Polygon(new LinearLineSegment(rightPoly)));
        });
    }

    public static IPath BuildRoundedRect(float x, float y, float w, float h, float cr)
    {
        if (cr < 0.5f)
            return new RectangularPolygon(x, y, w, h);

        cr = Math.Min(cr, Math.Min(w, h) / 2f);
        var pb = new PathBuilder();
        pb.MoveTo(new PointF(x + cr, y));
        pb.LineTo(new PointF(x + w - cr, y));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w, y + cr));
        pb.LineTo(new PointF(x + w, y + h - cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w - cr, y + h));
        pb.LineTo(new PointF(x + cr, y + h));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x, y + h - cr));
        pb.LineTo(new PointF(x, y + cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + cr, y));
        pb.CloseFigure();
        return pb.Build();
    }

    // =============================================
    // DITHERING
    // =============================================

    public static void DitherRegion(Image<Rgba32> image, LayoutConfig layout, RectangleF region)
    {
        int rx = Math.Max(0, (int)region.X);
        int ry = Math.Max(0, (int)region.Y);
        int rw = Math.Min(image.Width - rx, Math.Max(1, (int)Math.Ceiling(region.Width)));
        int rh = Math.Min(image.Height - ry, Math.Max(1, (int)Math.Ceiling(region.Height)));
        if (rw <= 0 || rh <= 0) return;

        var paletteColors = layout.ColorScheme.Palette
            .Select(hex => ParseColor(hex))
            .ToArray();
        if (paletteColors.Length == 0) return;

        using var sub = image.Clone(ctx => ctx.Crop(new SixLabors.ImageSharp.Rectangle(rx, ry, rw, rh)));
        sub.Mutate(ctx => ctx.Quantize(new PaletteQuantizer(
            new ReadOnlyMemory<Color>(paletteColors),
            new QuantizerOptions { Dither = KnownDitherings.JarvisJudiceNinke })));
        image.Mutate(ctx => ctx.DrawImage(sub, new SixLabors.ImageSharp.Point(rx, ry), 1f));
    }

    // =============================================
    // PIXEL POSITION RESOLUTION
    // =============================================

    public static (double X, double Y, double Width, double Height) ResolvePixelPosition(WidgetPositionConfig pos, LayoutConfig layout)
    {
        if (pos.PixelX.HasValue && pos.PixelY.HasValue && pos.PixelWidth.HasValue && pos.PixelHeight.HasValue)
            return (pos.PixelX.Value, pos.PixelY.Value, pos.PixelWidth.Value, pos.PixelHeight.Value);

        var padding = layout.CanvasPadding;
        var gap = layout.WidgetGap;
        var cols = Math.Max(1, layout.GridCols);
        var rows = Math.Max(1, layout.GridRows);
        var innerWidth = Math.Max(0, layout.Width - padding * 2 - gap * (cols - 1));
        var innerHeight = Math.Max(0, layout.Height - padding * 2 - gap * (rows - 1));
        var cellWidth = (double)innerWidth / cols;
        var cellHeight = (double)innerHeight / rows;

        var x = padding + pos.X * (cellWidth + gap);
        var y = padding + pos.Y * (cellHeight + gap);
        var w = pos.W * cellWidth + (pos.W - 1) * gap;
        var h = pos.H * cellHeight + (pos.H - 1) * gap;

        return (Math.Round(x * 100) / 100, Math.Round(y * 100) / 100, Math.Round(w * 100) / 100, Math.Round(h * 100) / 100);
    }

    // =============================================
    // FORMAT HELPERS
    // =============================================

    public static string FormatEventDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";
        if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dateStr.Length == 10
                ? dt.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
                : dt.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
        }
        return dateStr;
    }

    public static string FormatForecastTime(string? datetime, string mode)
    {
        if (string.IsNullOrEmpty(datetime)) return "";
        if (!DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return datetime;
        return mode switch
        {
            "hourly" => dt.ToString("HH:mm"),
            "weekly" => dt.ToString("ddd"),
            _ => dt.Day.ToString()
        };
    }

    public static string FormatCondition(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return "";
        return condition.ToLower() switch
        {
            "clear-night" => "Clear",
            "cloudy" => "Cloudy",
            "fog" => "Fog",
            "hail" => "Hail",
            "lightning" => "Storm",
            "lightning-rainy" => "Stormy",
            "partlycloudy" => "Pt. Cloudy",
            "pouring" => "Pouring",
            "rainy" => "Rainy",
            "snowy" => "Snowy",
            "snowy-rainy" => "Snowy Rain",
            "sunny" => "Sunny",
            "windy" => "Windy",
            "windy-variant" => "Windy",
            "exceptional" => "Exceptional",
            _ => condition
        };
    }

    public static string RoundNum(object? val)
    {
        if (val == null) return "";
        if (val is long l) return l.ToString();
        if (val is double d) return Math.Round(d).ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return Math.Round(num).ToString(CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }

    public static string RoundNumOneDecimal(object? val)
    {
        if (val == null) return "";
        if (val is double d) return Math.Round(d, 1).ToString(CultureInfo.InvariantCulture);
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return Math.Round(num, 1).ToString(CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }

    public static string GetDefaultSeriesColor(ColorSchemeConfig cs, int index)
    {
        var chartColors = cs.Palette
            .Where(c => !string.IsNullOrEmpty(c) && c != cs.Background && c != cs.CanvasBackgroundColor)
            .ToArray();
        if (chartColors.Length > 0)
            return chartColors[index % chartColors.Length];
        var fallback = new[] { "#ff0000", "#00ff00", "#0000ff", "#ffff00", "#ff00ff", "#00ffff" };
        return fallback[index % fallback.Length];
    }

    // =============================================
    // GRAPH HELPERS
    // =============================================

    public static IPath BuildSmoothedPath(PointF[] pts, float tension)
    {
        var builder = new PathBuilder();
        builder.StartFigure();

        for (int i = 0; i < pts.Length - 1; i++)
        {
            var p0 = pts[Math.Max(i - 1, 0)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(i + 2, pts.Length - 1)];

            var cp1 = new PointF(
                p1.X + (p2.X - p0.X) * tension / 3f,
                p1.Y + (p2.Y - p0.Y) * tension / 3f);
            var cp2 = new PointF(
                p2.X - (p3.X - p1.X) * tension / 3f,
                p2.Y - (p3.Y - p1.Y) * tension / 3f);

            builder.AddCubicBezier(p1, cp1, cp2, p2);
        }

        return builder.Build();
    }

    // =============================================
    // MARKDOWN HELPERS
    // =============================================

    private static readonly Regex HorizontalRulePattern = new(@"^[-*_]{3,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TaskListPattern = new(@"^[-*+]\s\[[ xX]\]\s", RegexOptions.Compiled);
    private static readonly Regex TaskCheckedPattern = new(@"^[-*+]\s\[[xX]\]", RegexOptions.Compiled);
    private static readonly Regex IndentedSubListPattern = new(@"^\s{2,}[-*+]\s", RegexOptions.Compiled);
    private static readonly Regex NumberedListPattern = new(@"^\d+\.\s", RegexOptions.Compiled);
    private static readonly Regex NumberedListCapture = new(@"^(\d+\.)\s(.*)$", RegexOptions.Compiled);

    // Inline markdown stripping patterns
    private static readonly Regex ImagePattern = new(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex LinkPattern = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex BoldItalic3Star = new(@"\*{3}(.+?)\*{3}", RegexOptions.Compiled);
    private static readonly Regex BoldItalic3Under = new(@"_{3}(.+?)_{3}", RegexOptions.Compiled);
    private static readonly Regex Bold2Star = new(@"\*{2}(.+?)\*{2}", RegexOptions.Compiled);
    private static readonly Regex Bold2Under = new(@"_{2}(.+?)_{2}", RegexOptions.Compiled);
    private static readonly Regex ItalicStar = new(@"\*(.+?)\*", RegexOptions.Compiled);
    private static readonly Regex ItalicUnder = new(@"(?<=\s|^)_(.+?)_(?=\s|$)", RegexOptions.Compiled);
    private static readonly Regex Strikethrough = new(@"~~(.+?)~~", RegexOptions.Compiled);
    private static readonly Regex InlineCode = new(@"`(.+?)`", RegexOptions.Compiled);

    public static bool IsHorizontalRule(string line) => HorizontalRulePattern.IsMatch(line);
    public static bool IsTaskListItem(string line) => TaskListPattern.IsMatch(line);
    public static bool IsTaskCheckedItem(string line) => TaskCheckedPattern.IsMatch(line);
    public static bool IsIndentedSubList(string line) => IndentedSubListPattern.IsMatch(line);
    public static bool IsNumberedList(string line) => NumberedListPattern.IsMatch(line);
    public static Match MatchNumberedList(string line) => NumberedListCapture.Match(line);

    public static string StripInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = ImagePattern.Replace(text, "$1");
        text = LinkPattern.Replace(text, "$1");
        text = BoldItalic3Star.Replace(text, "$1");
        text = BoldItalic3Under.Replace(text, "$1");
        text = Bold2Star.Replace(text, "$1");
        text = Bold2Under.Replace(text, "$1");
        text = ItalicStar.Replace(text, "$1");
        text = ItalicUnder.Replace(text, "$1");
        text = Strikethrough.Replace(text, "$1");
        text = InlineCode.Replace(text, "$1");

        return text;
    }

    public static bool IsEntirelyBold(string line)
    {
        var trimmed = line.Trim();
        return (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
            || (trimmed.StartsWith("__") && trimmed.EndsWith("__") && trimmed.Length > 4);
    }

    public enum HorizontalAlignment { Left, Center, Right }
}
