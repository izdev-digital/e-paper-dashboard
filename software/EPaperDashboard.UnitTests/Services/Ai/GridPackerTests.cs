using EPaperDashboard.Models;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class GridPackerTests
{
    private static GridPacker CreateSut() => new(NullLogger<GridPacker>.Instance);

    private static WidgetConfig Widget(string id, int w, int h) => new()
    {
        Id = id,
        Type = "test",
        Position = new WidgetPosition { W = w, H = h }
    };

    [Fact]
    public void Pack_NoWidgets_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var result = sut.Pack([], [], gridCols: 4, gridRows: 4);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Pack_WidgetFitsAtIdealSize_PlacesAtFirstAvailableOrigin()
    {
        var sut = CreateSut();
        var widget = Widget("w1", w: 2, h: 2);

        var result = sut.Pack([widget], [], gridCols: 4, gridRows: 4);

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
        widget.Position.X.Should().Be(0);
        widget.Position.Y.Should().Be(0);
        widget.Position.W.Should().Be(2);
        widget.Position.H.Should().Be(2);
    }

    [Fact]
    public void Pack_PinnedWidgetOccupiesCells_UnpinnedWidgetPlacedAroundIt()
    {
        var sut = CreateSut();
        var pinned = Widget("pinned", w: 2, h: 2);
        var widget = Widget("w1", w: 2, h: 2);

        var result = sut.Pack([widget], [pinned], gridCols: 4, gridRows: 4);

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
        widget.Position.X.Should().Be(2);
        widget.Position.Y.Should().Be(0);
    }

    [Fact]
    public void Pack_WidgetTallerThanAvailableRows_ShrinksHeightToFit()
    {
        var sut = CreateSut();
        var widget = Widget("w1", w: 2, h: 3);

        var result = sut.Pack([widget], [], gridCols: 4, gridRows: 2);

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
        widget.Position.W.Should().Be(2);
        widget.Position.H.Should().Be(2);
    }

    [Fact]
    public void Pack_GridFullyOccupiedByPinnedWidgets_UnpinnedWidgetIsSkipped()
    {
        var sut = CreateSut();
        var pinned = Widget("pinned", w: 2, h: 2);
        var widget = Widget("w1", w: 2, h: 2);

        var result = sut.Pack([widget], [pinned], gridCols: 2, gridRows: 2);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Pack_MultipleWidgetsFit_AllArePlacedWithoutOverlap()
    {
        var sut = CreateSut();
        var first = Widget("w1", w: 2, h: 2);
        var second = Widget("w2", w: 2, h: 2);

        var result = sut.Pack([first, second], [], gridCols: 4, gridRows: 2);

        result.Should().HaveCount(2);
        first.Position.X.Should().Be(0);
        second.Position.X.Should().Be(2);
    }
}
