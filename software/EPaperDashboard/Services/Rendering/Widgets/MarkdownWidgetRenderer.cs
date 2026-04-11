using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering.Widgets;

public sealed class MarkdownWidgetRenderer(RenderingUtilities utils) : IWidgetRenderer
{
    public string WidgetType => "markdown";

    public Task RenderAsync(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, SsrData data, RectangleF contentRect, CancellationToken cancellationToken = default)
    {
        RenderMarkdown(image, widget, layout, contentRect);
        return Task.CompletedTask;
    }

    internal void RenderMarkdown(Image<Rgba32> image, WidgetConfigEntry widget, LayoutConfig layout, RectangleF contentRect)
    {
        var ctx = WidgetRenderContext.Create(widget, layout);
        var textColor = ctx.TextColor;
        var iconColor = ctx.IconColor;
        var textFontSize = ctx.TextFontSize;
        var textFontWeight = ctx.TextFontWeight;
        var titleFontSize = ctx.TitleFontSize;
        var titleFontWeight = ctx.TitleFontWeight;

        var content = RenderingUtilities.GetStringProp(widget.Config, "content") ?? "";
        if (string.IsNullOrEmpty(content)) return;

        var mdPadding = 8f;
        var innerRect = new RectangleF(
            contentRect.X + mdPadding,
            contentRect.Y + mdPadding,
            Math.Max(0, contentRect.Width - mdPadding * 2),
            Math.Max(0, contentRect.Height - mdPadding * 2));

        var elementSpacing = 4f;
        var lines = content.Split('\n');
        float yOffset = innerRect.Y;
        var inCodeBlock = false;
        const int maxLines = 1000;
        var lineCount = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (yOffset > innerRect.Bottom || ++lineCount > maxLines) break;

            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                yOffset += elementSpacing;
                continue;
            }

            if (inCodeBlock)
            {
                var codeFont = utils.GetFont(textFontSize - 1, textFontWeight);
                var codeGlyph = TextMeasurer.MeasureSize("Ay", new TextOptions(codeFont)).Height;
                var codeLineH = codeGlyph + 2;
                if (yOffset + codeLineH > innerRect.Bottom) break;
                TextDrawing.DrawTextEllipsis(image, line, codeFont, ColorUtils.WithOpacity(textColor, 0.85f),
                    new RectangleF(innerRect.X + 8, yOffset, innerRect.Width - 8, codeLineH));
                yOffset += codeLineH;
                continue;
            }

            int fontSize;
            int fontWeight;
            string text;
            float xIndent = 0;
            float lineHeightMultiplier;
            int maxWrapLines;
            bool isBlockquote = false;
            bool isTaskItem = false;
            bool isTaskChecked = false;

            if (line.StartsWith("#### "))
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.3f;
                text = MarkdownHelpers.StripInlineMarkdown(line[5..]); maxWrapLines = 2;
            }
            else if (line.StartsWith("### "))
            {
                fontSize = (int)(textFontSize * 1.1); fontWeight = textFontWeight; lineHeightMultiplier = 1.3f;
                text = MarkdownHelpers.StripInlineMarkdown(line[4..]); maxWrapLines = 2;
            }
            else if (line.StartsWith("## "))
            {
                fontSize = (int)(titleFontSize * 1.0); fontWeight = titleFontWeight; lineHeightMultiplier = 1.3f;
                text = MarkdownHelpers.StripInlineMarkdown(line[3..]); maxWrapLines = 2;
            }
            else if (line.StartsWith("# "))
            {
                fontSize = (int)(titleFontSize * 1.2); fontWeight = titleFontWeight; lineHeightMultiplier = 1.3f;
                text = MarkdownHelpers.StripInlineMarkdown(line[2..]); maxWrapLines = 3;
            }
            else if (MarkdownHelpers.IsHorizontalRule(line))
            {
                yOffset += 8;
                var lineY = yOffset;
                image.Mutate(ctx => ctx.DrawLine(
                    ColorUtils.WithOpacity(textColor, 0.3f), 1f,
                    new PointF(innerRect.X, lineY),
                    new PointF(innerRect.Right, lineY)));
                yOffset += 8 + elementSpacing;
                continue;
            }
            else if (MarkdownHelpers.IsTaskListItem(line))
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.5f;
                isTaskItem = true;
                isTaskChecked = MarkdownHelpers.IsTaskCheckedItem(line);
                text = MarkdownHelpers.StripInlineMarkdown(line[6..]); maxWrapLines = 3;
            }
            else if (line.StartsWith("> ") || line == ">")
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.5f;
                text = MarkdownHelpers.StripInlineMarkdown(line.Length > 2 ? line[2..] : "");
                xIndent = 3 + 8; maxWrapLines = 10; isBlockquote = true;
            }
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.5f;
                text = $"• {MarkdownHelpers.StripInlineMarkdown(line[2..])}"; maxWrapLines = 5;
            }
            else if (MarkdownHelpers.IsIndentedSubList(line))
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.5f;
                var stripped = line.TrimStart();
                text = $"  ◦ {MarkdownHelpers.StripInlineMarkdown(stripped[2..])}"; maxWrapLines = 5;
            }
            else if (MarkdownHelpers.IsNumberedList(line))
            {
                fontSize = textFontSize; fontWeight = textFontWeight; lineHeightMultiplier = 1.5f;
                var match = MarkdownHelpers.MatchNumberedList(line);
                text = match.Success
                    ? $"{match.Groups[1].Value} {MarkdownHelpers.StripInlineMarkdown(match.Groups[2].Value)}"
                    : MarkdownHelpers.StripInlineMarkdown(line);
                maxWrapLines = 5;
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                yOffset += textFontSize * 0.5f;
                continue;
            }
            else
            {
                fontSize = textFontSize;
                fontWeight = MarkdownHelpers.IsEntirelyBold(line) ? titleFontWeight : textFontWeight;
                lineHeightMultiplier = 1.5f;
                text = MarkdownHelpers.StripInlineMarkdown(line);
                maxWrapLines = 50;
            }

            var font = utils.GetFont(fontSize, fontWeight);
            var availableHeight = innerRect.Bottom - yOffset;
            if (availableHeight <= 0) break;

            var glyphHeight = TextMeasurer.MeasureSize("Ay", new TextOptions(font)).Height;
            var extraSpacing = Math.Max(0, fontSize * lineHeightMultiplier - glyphHeight);

            float startY = yOffset;
            float drawX = innerRect.X + xIndent;
            float drawW = innerRect.Width - xIndent;

            if (isTaskItem)
            {
                var checkSize = (float)fontSize;
                var checkIcon = isTaskChecked ? "fa-square-check" : "fa-square";
                var checkColor = isTaskChecked ? ColorUtils.WithOpacity(iconColor, 0.6f) : iconColor;
                var checkBounds = new RectangleF(drawX, yOffset + 1, checkSize, checkSize);
                utils.DrawFaIcon(image, checkIcon, checkColor, checkBounds);
                drawX += checkSize + 4;
                drawW -= checkSize + 4;
                var taskColor = isTaskChecked ? ColorUtils.WithOpacity(textColor, 0.6f) : textColor;
                var textRect = new RectangleF(drawX, yOffset, drawW, availableHeight);
                var consumedHeight = utils.DrawTextWithInlineIcons(image, text, font, taskColor, iconColor, textRect, maxWrapLines, extraSpacing);

                if (isTaskChecked && consumedHeight > 0)
                {
                    var strikeY = yOffset + consumedHeight / 2f;
                    var sw = Math.Min(TextMeasurer.MeasureSize(text, new TextOptions(font)).Width, drawW);
                    image.Mutate(ctx => ctx.DrawLine(taskColor, 1f,
                        new PointF(drawX, strikeY), new PointF(drawX + sw, strikeY)));
                }

                yOffset += consumedHeight + elementSpacing;
                continue;
            }

            var mainTextRect = new RectangleF(drawX, yOffset, drawW, availableHeight);
            var mainColor = isBlockquote ? ColorUtils.WithOpacity(textColor, 0.8f) : textColor;
            var mainConsumed = utils.DrawTextWithInlineIcons(image, text, font, mainColor, iconColor, mainTextRect, maxWrapLines, extraSpacing);

            if (isBlockquote && mainConsumed > 0)
            {
                var barX = innerRect.X + 1.5f;
                var bqColor = ColorUtils.WithOpacity(textColor, 0.8f);
                image.Mutate(ctx => ctx.DrawLine(
                    bqColor, 3f,
                    new PointF(barX, startY),
                    new PointF(barX, startY + mainConsumed)));
            }

            yOffset += mainConsumed + elementSpacing;
        }
    }
}
