using System.Text.Json;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class ColorUtilsTests
{
    [Fact]
    public void ParseColor_ValidHex_ReturnsMatchingColor()
    {
        ColorUtils.ParseColor("#ff0000").Should().Be(Color.Red);
    }

    [Fact]
    public void ParseColor_NullOrEmpty_ReturnsBlack()
    {
        ColorUtils.ParseColor("").Should().Be(Color.Black);
        ColorUtils.ParseColor(null!).Should().Be(Color.Black);
    }

    [Fact]
    public void ParseColor_InvalidHex_ReturnsBlackInsteadOfThrowing()
    {
        ColorUtils.ParseColor("not-a-color").Should().Be(Color.Black);
    }

    [Fact]
    public void WithOpacity_ScalesAlphaChannel()
    {
        var opaque = new Color(new Rgba32(10, 20, 30, 255));

        var halfOpacity = ColorUtils.WithOpacity(opaque, 0.5f);

        halfOpacity.ToPixel<Rgba32>().A.Should().Be((byte)(255 * 0.5f));
    }

    [Fact]
    public void WithOpacity_ClampsOutOfRangeValues()
    {
        var opaque = new Color(new Rgba32(10, 20, 30, 200));

        ColorUtils.WithOpacity(opaque, 2f).ToPixel<Rgba32>().A.Should().Be(200);
        ColorUtils.WithOpacity(opaque, -1f).ToPixel<Rgba32>().A.Should().Be(0);
    }

    [Fact]
    public void ResolveWidgetColor_OverridePresent_UsesOverrideInsteadOfScheme()
    {
        var overrides = new WidgetColorOverridesConfig("#111111", null, null, null, null);
        var widget = new WidgetConfigEntry("id", "type", new WidgetPositionConfig(0, 0, 0, 0), default, overrides);
        var scheme = new ColorSchemeConfig("name", null, [], "#000000", "#000000", "#222222", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000");
        var layout = new LayoutConfig(100, 100, 4, 4, scheme, [], 0, 0, 0, 0, 0, 0, 0, 0);

        var result = ColorUtils.ResolveWidgetColor(
            widget, layout, s => s.WidgetBackgroundColor, o => o?.WidgetBackgroundColor);

        result.Should().Be(ColorUtils.ParseColor("#111111"));
    }

    [Fact]
    public void ResolveWidgetColor_NoOverride_FallsBackToSchemeColor()
    {
        var widget = new WidgetConfigEntry("id", "type", new WidgetPositionConfig(0, 0, 0, 0), default, null);
        var scheme = new ColorSchemeConfig("name", null, [], "#000000", "#000000", "#222222", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000");
        var layout = new LayoutConfig(100, 100, 4, 4, scheme, [], 0, 0, 0, 0, 0, 0, 0, 0);

        var result = ColorUtils.ResolveWidgetColor(
            widget, layout, s => s.WidgetBackgroundColor, o => o?.WidgetBackgroundColor);

        result.Should().Be(ColorUtils.ParseColor("#222222"));
    }

    [Fact]
    public void GetDefaultSeriesColor_PaletteExcludingBackgroundColors_CyclesThroughRemainingColors()
    {
        var scheme = new ColorSchemeConfig(
            "name", null, ["#000000", "#ff0000", "#00ff00"], "#000000", "#000000",
            "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000");

        ColorUtils.GetDefaultSeriesColor(scheme, 0).Should().Be("#ff0000");
        ColorUtils.GetDefaultSeriesColor(scheme, 1).Should().Be("#00ff00");
        ColorUtils.GetDefaultSeriesColor(scheme, 2).Should().Be("#ff0000"); // wraps around
    }

    [Fact]
    public void GetDefaultSeriesColor_EmptyPalette_UsesFallbackColors()
    {
        var scheme = new ColorSchemeConfig(
            "name", null, [], "#000000", "#000000",
            "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000", "#000000");

        ColorUtils.GetDefaultSeriesColor(scheme, 0).Should().Be("#ff0000");
    }
}
