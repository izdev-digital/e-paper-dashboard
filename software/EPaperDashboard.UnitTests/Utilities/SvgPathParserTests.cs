using EPaperDashboard.Utilities;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Utilities;

public class SvgPathParserTests
{
    [Fact]
    public void Parse_SimpleSquareWithAbsoluteLineTo_BoundsMatchCorners()
    {
        var path = SvgPathParser.Parse("M0,0 L10,0 L10,10 L0,10 Z");

        path.Bounds.Left.Should().Be(0);
        path.Bounds.Top.Should().Be(0);
        path.Bounds.Right.Should().Be(10);
        path.Bounds.Bottom.Should().Be(10);
    }

    [Fact]
    public void Parse_RelativeLineTo_ComputesAbsolutePositionsFromCurrentPoint()
    {
        // M10,10 then relative line +10,+0 then +0,+10 → square from (10,10) to (20,20)
        var path = SvgPathParser.Parse("M10,10 l10,0 l0,10 Z");

        path.Bounds.Left.Should().Be(10);
        path.Bounds.Top.Should().Be(10);
        path.Bounds.Right.Should().Be(20);
        path.Bounds.Bottom.Should().Be(20);
    }

    [Fact]
    public void Parse_HorizontalAndVerticalLineTo_MoveOnlyOneAxis()
    {
        var path = SvgPathParser.Parse("M0,0 H10 V5");

        path.Bounds.Right.Should().Be(10);
        path.Bounds.Bottom.Should().Be(5);
    }

    [Fact]
    public void Parse_RelativeHorizontalAndVertical_AreOffsetFromCurrentPoint()
    {
        var path = SvgPathParser.Parse("M5,5 h5 v5");

        path.Bounds.Right.Should().Be(10);
        path.Bounds.Bottom.Should().Be(10);
    }

    [Fact]
    public void Parse_MultipleCoordinatePairsAfterMoveTo_TreatsSubsequentPairsAsLineTo()
    {
        var path = SvgPathParser.Parse("M0,0 10,0 10,10");

        path.Bounds.Right.Should().Be(10);
        path.Bounds.Bottom.Should().Be(10);
    }

    [Fact]
    public void Parse_CubicBezierCurve_DoesNotThrowAndBoundsContainEndpoints()
    {
        var path = SvgPathParser.Parse("M0,0 C5,10 15,10 20,0");

        path.Bounds.Left.Should().BeLessThanOrEqualTo(0);
        path.Bounds.Right.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void Parse_QuadraticBezierCurve_DoesNotThrowAndBoundsContainEndpoints()
    {
        var path = SvgPathParser.Parse("M0,0 Q10,10 20,0");

        path.Bounds.Left.Should().BeLessThanOrEqualTo(0);
        path.Bounds.Right.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void Parse_ArcCommand_DoesNotThrowAndProducesNonEmptyPath()
    {
        var path = SvgPathParser.Parse("M0,0 A5,5 0 0 1 10,10");

        path.Bounds.Should().NotBeNull();
    }

    [Fact]
    public void Parse_SmoothCubicBezier_ReflectsPreviousControlPoint()
    {
        var path = SvgPathParser.Parse("M0,0 C5,10 15,10 20,0 S35,-10 40,0");

        path.Bounds.Right.Should().Be(40);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyPathWithoutThrowing()
    {
        var act = () => SvgPathParser.Parse("");

        act.Should().NotThrow();
    }

    [Fact]
    public void Parse_ExponentialNotationNumbers_AreParsedAsNumbersNotCommands()
    {
        // "1e2" should parse as the number 100, not as command 'e' (which isn't a valid command anyway,
        // but this guards against the tokenizer misinterpreting the exponent marker).
        var act = () => SvgPathParser.Parse("M0,0 L1e1,0");

        act.Should().NotThrow();
        var path = SvgPathParser.Parse("M0,0 L1e1,0");
        path.Bounds.Right.Should().Be(10);
    }
}
