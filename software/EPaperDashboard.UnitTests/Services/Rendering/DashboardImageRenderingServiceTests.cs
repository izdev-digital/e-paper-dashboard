using System.Text.Json;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using LayoutConfig = EPaperDashboard.Models.LayoutConfig;
using RenderingLayoutConfig = EPaperDashboard.Models.Rendering.LayoutConfig;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class DashboardImageRenderingServiceTests
{
    private readonly Mock<ISsrDataProvider> _ssrDataProvider = new();

    // FontAwesomeIconRegistry gracefully falls back to built-in icons when fa-icons.json
    // isn't found, so a mocked IWebHostEnvironment with no real wwwroot is sufficient here.
    private static RenderingUtilities CreateRenderingUtils()
    {
        var iconRegistry = new FontAwesomeIconRegistry(Mock.Of<IWebHostEnvironment>(), NullLogger<FontAwesomeIconRegistry>.Instance);
        var fontFamily = SystemFonts.Families.First();
        return new RenderingUtilities(fontFamily, iconRegistry);
    }

    private DashboardImageRenderingService CreateSut(params IWidgetRenderer[] renderers) => new(
        _ssrDataProvider.Object,
        NullLogger<DashboardImageRenderingService>.Instance,
        CreateRenderingUtils(),
        renderers,
        new MemoryCache(new MemoryCacheOptions()));

    private static LayoutConfig SimpleLayout(string widgetType = "header") => new()
    {
        Width = 200,
        Height = 100,
        GridCols = 4,
        GridRows = 2,
        Widgets =
        [
            new WidgetConfig
            {
                Id = "w1",
                Type = widgetType,
                Position = new WidgetPosition { X = 0, Y = 0, W = 4, H = 2, PixelX = 0, PixelY = 0, PixelWidth = 200, PixelHeight = 100 },
                // Matches what production widget-creation paths (e.g. AiResponseParser.Parse) always
                // set — a default(JsonElement) here reproduces a real ComputeLayoutHash crash (see
                // ComputeLayoutHash_WidgetWithUninitializedConfig_ThrowsInsteadOfDegrading below).
                Config = JsonSerializer.SerializeToElement(new { })
            }
        ]
    };

    // NOTE: this documents a real production bug, not a desired behavior. ComputeLayoutHash does
    // `JsonSerializer.Serialize(layoutConfig)` over the whole layout, including each widget's
    // Config (a JsonElement). A widget whose Config was never assigned defaults to
    // default(JsonElement) (ValueKind.Undefined), and serializing that throws InvalidOperationException
    // — which means RenderDashboardImageAsync crashes instead of rendering a placeholder/error
    // indicator for that one widget. Every other widget-config code path in this codebase happens to
    // always assign a real JsonElement (e.g. AiResponseParser.Parse defaults to `{}`), so this may
    // not be reachable today, but it's a latent crash if a widget is ever persisted without Config
    // (e.g. a manual DB edit, a future code path, or a schema migration gap). Flagging rather than
    // fixing since it's outside the scope of adding test coverage.
    [Fact]
    public async Task ComputeLayoutHash_WidgetWithUninitializedConfig_ThrowsInsteadOfDegrading()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();
        var layout = SimpleLayout();
        layout.Widgets[0].Config = default; // uninitialized JsonElement, as could come from a bare `new WidgetConfig()`

        var act = async () => await sut.RenderDashboardImageAsync("dash1", layout);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RenderDashboardImageAsync_ReturnsImageWithConfiguredDimensions()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();

        using var image = await sut.RenderDashboardImageAsync("dash1", SimpleLayout());

        image.Width.Should().Be(200);
        image.Height.Should().Be(100);
    }

    [Fact]
    public async Task RenderDashboardImageAsync_SecondCallWithinCacheDuration_DoesNotRefetchSsrData()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();
        var layout = SimpleLayout();

        using var first = await sut.RenderDashboardImageAsync("dash1", layout);
        using var second = await sut.RenderDashboardImageAsync("dash1", layout);

        _ssrDataProvider.Verify(
            p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenderDashboardImageAsync_DifferentLayout_BustsCacheAndRefetches()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();

        using var first = await sut.RenderDashboardImageAsync("dash1", SimpleLayout("header"));
        using var second = await sut.RenderDashboardImageAsync("dash1", SimpleLayout("markdown"));

        _ssrDataProvider.Verify(
            p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RenderDashboardImageAsync_UnknownWidgetType_RendersPlaceholderInsteadOfThrowing()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut(); // no renderers registered at all

        var act = async () => await sut.RenderDashboardImageAsync("dash1", SimpleLayout("some-unregistered-type"));

        (await act.Should().NotThrowAsync()).Which.Should().NotBeNull();
    }

    [Fact]
    public async Task RenderDashboardImageAsync_RendererThrows_RendersErrorIndicatorInsteadOfPropagating()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var throwingRenderer = new Mock<IWidgetRenderer>();
        throwingRenderer.SetupGet(r => r.WidgetType).Returns("header");
        throwingRenderer
            .Setup(r => r.RenderAsync(
                It.IsAny<SixLabors.ImageSharp.Image<Rgba32>>(), It.IsAny<WidgetConfigEntry>(), It.IsAny<RenderingLayoutConfig>(),
                It.IsAny<SsrData>(), It.IsAny<SixLabors.ImageSharp.RectangleF>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateSut(throwingRenderer.Object);

        var act = async () => await sut.RenderDashboardImageAsync("dash1", SimpleLayout("header"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RenderDashboardImageAsync_MatchingRenderer_IsInvokedForItsWidgetType()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(It.IsAny<string>(), It.IsAny<RenderingLayoutConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SsrData());
        var renderer = new Mock<IWidgetRenderer>();
        renderer.SetupGet(r => r.WidgetType).Returns("header");
        renderer
            .Setup(r => r.RenderAsync(
                It.IsAny<SixLabors.ImageSharp.Image<Rgba32>>(), It.IsAny<WidgetConfigEntry>(), It.IsAny<RenderingLayoutConfig>(),
                It.IsAny<SsrData>(), It.IsAny<SixLabors.ImageSharp.RectangleF>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(renderer.Object);

        using var image = await sut.RenderDashboardImageAsync("dash1", SimpleLayout("header"));

        renderer.Verify(r => r.RenderAsync(
            It.IsAny<SixLabors.ImageSharp.Image<Rgba32>>(),
            It.Is<WidgetConfigEntry>(w => w.Id == "w1"),
            It.IsAny<RenderingLayoutConfig>(),
            It.IsAny<SsrData>(),
            It.IsAny<SixLabors.ImageSharp.RectangleF>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
