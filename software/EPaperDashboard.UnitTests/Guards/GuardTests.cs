using EPaperDashboard.Guards;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Guards;

public class GuardTests
{
    [Fact]
    public void NeitherNullNorWhitespace_ValidValue_ReturnsIt()
    {
        Guard.NeitherNullNorWhitespace("hello").Should().Be("hello");
    }

    [Fact]
    public void NeitherNullNorWhitespace_Null_ThrowsArgumentNullException()
    {
        string? value = null;
        var act = () => Guard.NeitherNullNorWhitespace(value);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NeitherNullNorWhitespace_EmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        var act = () => Guard.NeitherNullNorWhitespace(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NotNull_NonNullValue_ReturnsIt()
    {
        Guard.NotNull("value").Should().Be("value");
    }

    [Fact]
    public void NotNull_Null_ThrowsArgumentNullExceptionWithParameterName()
    {
        string? value = null;
        var act = () => Guard.NotNull(value);

        act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }
}
