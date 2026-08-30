using System.Text.Json;
using EPaperDashboard.Models;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Providers;
using EPaperDashboard.Services.Rendering;
using EPaperDashboard.Services.Rendering.Widgets;
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

    // Regression test for a real bug: ComputeLayoutHash serializes the whole layout, including each
    // widget's Config (a JsonElement). Config previously had no field initializer, so it defaulted to
    // ValueKind.Undefined for any widget constructed without explicitly setting it (e.g. deserialized
    // from a document predating this field) — and System.Text.Json throws InvalidOperationException
    // when asked to serialize an Undefined JsonElement, crashing the entire render instead of just
    // that widget. Fixed by giving WidgetConfig.Config a real empty-object default (Models/LayoutConfig.cs).
    [Fact]
    public async Task RenderDashboardImageAsync_WidgetConstructedWithoutExplicitConfig_DoesNotThrow()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(
                It.IsAny<string>(),
                It.IsAny<RenderingLayoutConfig>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();
        var layout = SimpleLayout();
        layout.Widgets[0] = new WidgetConfig
        {
            Id = "w1",
            Type = "header",
            Position = layout.Widgets[0].Position
            // Config intentionally left unset.
        };

        var act = async () => await sut.RenderDashboardImageAsync("dash1", layout);

        await act.Should().NotThrowAsync();
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
    public async Task RenderDashboardImageAsync_BypassCache_RefetchesSsrData()
    {
        _ssrDataProvider
            .Setup(p => p.FetchSsrDataAsync(
                It.IsAny<string>(),
                It.IsAny<RenderingLayoutConfig>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new SsrData());
        var sut = CreateSut();
        var layout = SimpleLayout();

        using var first = await sut.RenderDashboardImageAsync("dash1", layout);
        using var second = await sut.RenderDashboardImageAsync("dash1", layout, bypassCache: true);

        _ssrDataProvider.Verify(
            p => p.FetchSsrDataAsync(
                It.IsAny<string>(),
                It.IsAny<RenderingLayoutConfig>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()),
            Times.Exactly(2));
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
        var layout = SimpleLayout("header");
        layout.Widgets[0].ShowTitle = false;

        using var image = await sut.RenderDashboardImageAsync("dash1", layout);

        renderer.Verify(r => r.RenderAsync(
            It.IsAny<SixLabors.ImageSharp.Image<Rgba32>>(),
            It.Is<WidgetConfigEntry>(w => w.Id == "w1" && !w.ShowTitle),
            It.IsAny<RenderingLayoutConfig>(),
            It.IsAny<SsrData>(),
            It.IsAny<SixLabors.ImageSharp.RectangleF>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResolveWidgetGeometry_HeaderUsesRendererElementPositionsAndOriginalBadgeIndexes()
    {
        var sut = CreateSut(new HeaderWidgetRenderer(CreateRenderingUtils()));
        var layout = SimpleLayout("header");
        layout.Widgets[0].Config = JsonSerializer.SerializeToElement(new
        {
            title = "Dashboard",
            titleX = 10,
            titleY = 12,
            titleW = 60,
            titleH = 24,
            badges = new object[]
            {
                new { entityId = "", icon = "" },
                new { entityId = "sensor.room", x = 20, y = 50, w = 30, h = 15 }
            }
        });

        var geometry = sut.ResolveWidgetGeometry(layout).Should().ContainSingle().Subject;

        geometry.Editable.Should().BeTrue();
        geometry.Elements.Should().HaveCount(3);
        geometry.Elements.Should().ContainSingle(element =>
            element.Id == "title"
            && element.Kind == "title"
            && element.Position == new RenderRectangle(10, 12, 60, 24));
        geometry.Elements.Should().ContainSingle(element =>
            element.Id == "badge-0"
            && element.Kind == "badge"
            && element.Index == 0
            && element.Position == new RenderRectangle(0, 0, 22, 30));
        geometry.Elements.Should().ContainSingle(element =>
            element.Id == "badge-1"
            && element.Kind == "badge"
            && element.Index == 1
            && element.Position == new RenderRectangle(20, 50, 30, 15));
    }

    [Fact]
    public void ResolveWidgetGeometry_WeatherReturnsOnlyVisibleEditableItems()
    {
        var sut = CreateSut(new WeatherWidgetRenderer(CreateRenderingUtils()));
        var layout = SimpleLayout("weather");
        layout.Widgets[0].Config = JsonSerializer.SerializeToElement(new
        {
            entityId = "weather.home",
            items = new object[]
            {
                new { type = "temperature", visible = false, x = 0, y = 0, w = 50, h = 20 },
                new { type = "condition", visible = true, x = 12, y = 34, w = 56, h = 20 }
            }
        });

        var geometry = sut.ResolveWidgetGeometry(layout).Should().ContainSingle().Subject;

        geometry.Editable.Should().BeTrue();
        geometry.Elements.Should().ContainSingle();
        geometry.Elements[0].Id.Should().Be("weather-item-1");
        geometry.Elements[0].Index.Should().Be(1);
        geometry.Elements[0].Position.Should().Be(new RenderRectangle(12, 34, 56, 20));
    }
}
