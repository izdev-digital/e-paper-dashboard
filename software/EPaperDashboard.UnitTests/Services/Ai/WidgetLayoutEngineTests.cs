using System.Text.Json;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class WidgetLayoutEngineTests
{
    private static WidgetLayoutEngine CreateSut() => new();

    private static WidgetConfig Widget(string type, object? config = null) => new()
    {
        Id = "w1",
        Type = type,
        Config = JsonSerializer.SerializeToElement(config ?? new { })
    };

    [Fact]
    public void ComputeSizes_HeaderWidget_SpansFullGridWidthAndOneRow()
    {
        var sut = CreateSut();
        var widget = Widget("header");

        sut.ComputeSizes([widget], new AiDataSnapshot(), new LayoutConfig(), gridCols: 8);

        widget.Position.W.Should().Be(8);
        widget.Position.H.Should().Be(1);
    }

    [Fact]
    public void ComputeSizes_AppIconWidget_IsSingleCell()
    {
        var sut = CreateSut();
        var widget = Widget("app-icon");

        sut.ComputeSizes([widget], new AiDataSnapshot(), new LayoutConfig(), gridCols: 8);

        widget.Position.W.Should().Be(1);
        widget.Position.H.Should().Be(1);
    }

    [Fact]
    public void ComputeSizes_UnknownWidgetType_FallsBackToDefaultSize()
    {
        var sut = CreateSut();
        var widget = Widget("some-future-widget");

        sut.ComputeSizes([widget], new AiDataSnapshot(), new LayoutConfig(), gridCols: 8);

        widget.Position.W.Should().Be(4);
        widget.Position.H.Should().Be(2);
    }

    [Fact]
    public void ComputeSizes_CalendarWidgetWithMoreEvents_IsTallerThanWithFewerEvents()
    {
        var sut = CreateSut();
        var fewEventsWidget = Widget("calendar", new { entityId = "calendar.a" });
        var manyEventsWidget = Widget("calendar", new { entityId = "calendar.b" });
        var aiData = new AiDataSnapshot
        {
            CalendarEvents =
            {
                ["calendar.a"] = [new CalendarEvent()],
                ["calendar.b"] = Enumerable.Range(0, 20).Select(_ => new CalendarEvent()).ToList()
            }
        };

        sut.ComputeSizes([fewEventsWidget, manyEventsWidget], aiData, new LayoutConfig(), gridCols: 8);

        manyEventsWidget.Position.H.Should().BeGreaterThan(fewEventsWidget.Position.H);
    }

    [Fact]
    public void ComputeSizes_MarkdownWidgetWithLongerContent_IsWiderOrTallerThanShortContent()
    {
        var sut = CreateSut();
        var shortWidget = Widget("markdown", new { content = "short" });
        var longWidget = Widget("markdown", new { content = new string('x', 500) });

        sut.ComputeSizes([shortWidget, longWidget], new AiDataSnapshot(), new LayoutConfig(), gridCols: 8);

        (longWidget.Position.W * longWidget.Position.H).Should()
            .BeGreaterThan(shortWidget.Position.W * shortWidget.Position.H);
    }

    [Fact]
    public void ComputeSizes_AllWidgets_ProduceSizesWithinGridBounds()
    {
        var sut = CreateSut();
        var widgets = new List<WidgetConfig>
        {
            Widget("header"), Widget("app-icon"), Widget("weather"), Widget("weather-forecast"),
            Widget("rss-feed"), Widget("graph"), Widget("markdown", new { content = "x" })
        };

        sut.ComputeSizes(widgets, new AiDataSnapshot(), new LayoutConfig(), gridCols: 8);

        widgets.Should().OnlyContain(w => w.Position.W >= 1 && w.Position.W <= 8 && w.Position.H >= 1);
    }
}
