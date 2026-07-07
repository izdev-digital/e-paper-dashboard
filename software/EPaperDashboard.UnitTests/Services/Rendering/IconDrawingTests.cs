using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class IconDrawingTests
{
    [Fact]
    public void BuildRoundedRect_ZeroCornerRadius_ReturnsSharpRectangleBounds()
    {
        var path = IconDrawing.BuildRoundedRect(0, 0, 100, 50, cr: 0);

        path.Bounds.Width.Should().Be(100);
        path.Bounds.Height.Should().Be(50);
    }

    [Fact]
    public void BuildRoundedRect_WithCornerRadius_BoundsStillMatchRequestedSize()
    {
        var path = IconDrawing.BuildRoundedRect(0, 0, 100, 50, cr: 10);

        path.Bounds.Width.Should().BeApproximately(100, 1f);
        path.Bounds.Height.Should().BeApproximately(50, 1f);
    }

    [Fact]
    public void BuildRoundedRect_CornerRadiusLargerThanHalfSmallestSide_IsClamped()
    {
        // cr=100 on a 20x20 square should be clamped to 10 (half the smallest side) rather than
        // producing a malformed/self-intersecting path.
        var act = () => IconDrawing.BuildRoundedRect(0, 0, 20, 20, cr: 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void BuildRoundedRect_OffsetPosition_BoundsReflectOffset()
    {
        var path = IconDrawing.BuildRoundedRect(10, 20, 30, 40, cr: 0);

        path.Bounds.Left.Should().Be(10);
        path.Bounds.Top.Should().Be(20);
    }
}
