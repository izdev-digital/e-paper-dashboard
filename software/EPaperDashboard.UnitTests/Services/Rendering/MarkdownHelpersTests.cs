using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class MarkdownHelpersTests
{
    [Theory]
    [InlineData("---", true)]
    [InlineData("***", true)]
    [InlineData("___", true)]
    [InlineData("-- ", false)]
    [InlineData("plain text", false)]
    public void IsHorizontalRule_MatchesThreeOrMoreRuleCharacters(string line, bool expected) =>
        MarkdownHelpers.IsHorizontalRule(line).Should().Be(expected);

    [Theory]
    [InlineData("- [ ] task", true)]
    [InlineData("- [x] done", true)]
    [InlineData("- [X] done", true)]
    [InlineData("- no checkbox", false)]
    public void IsTaskListItem_MatchesCheckboxSyntax(string line, bool expected) =>
        MarkdownHelpers.IsTaskListItem(line).Should().Be(expected);

    [Theory]
    [InlineData("- [x] done", true)]
    [InlineData("- [X] done", true)]
    [InlineData("- [ ] pending", false)]
    public void IsTaskCheckedItem_OnlyMatchesCheckedBoxes(string line, bool expected) =>
        MarkdownHelpers.IsTaskCheckedItem(line).Should().Be(expected);

    [Theory]
    [InlineData("  - nested", true)]
    [InlineData("- top level", false)]
    public void IsIndentedSubList_RequiresAtLeastTwoLeadingSpaces(string line, bool expected) =>
        MarkdownHelpers.IsIndentedSubList(line).Should().Be(expected);

    [Theory]
    [InlineData("1. first", true)]
    [InlineData("42. answer", true)]
    [InlineData("not numbered", false)]
    public void IsNumberedList_MatchesDigitDotSpace(string line, bool expected) =>
        MarkdownHelpers.IsNumberedList(line).Should().Be(expected);

    [Fact]
    public void MatchNumberedList_CapturesNumberAndContent()
    {
        var match = MarkdownHelpers.MatchNumberedList("3. buy milk");

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("3.");
        match.Groups[2].Value.Should().Be("buy milk");
    }

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("*italic*", "italic")]
    [InlineData("__bold__", "bold")]
    [InlineData("~~strike~~", "strike")]
    [InlineData("`code`", "code")]
    [InlineData("[link text](http://example.com)", "link text")]
    [InlineData("![alt text](http://example.com/img.png)", "alt text")]
    public void StripInlineMarkdown_RemovesFormattingKeepingInnerText(string input, string expected) =>
        MarkdownHelpers.StripInlineMarkdown(input).Should().Be(expected);

    [Fact]
    public void StripInlineMarkdown_NullOrEmpty_ReturnsInputUnchanged()
    {
        MarkdownHelpers.StripInlineMarkdown("").Should().Be("");
    }

    [Fact]
    public void StripEmoji_RemovesEmojiAndTrimsResult()
    {
        MarkdownHelpers.StripEmoji("Hello 😀 world").Should().Be("Hello  world");
    }

    [Theory]
    [InlineData("**entirely bold**", true)]
    [InlineData("__entirely bold__", true)]
    [InlineData("partially **bold** text", false)]
    [InlineData("plain", false)]
    public void IsEntirelyBold_ChecksWholeLineWrappedInBoldMarkers(string line, bool expected) =>
        MarkdownHelpers.IsEntirelyBold(line).Should().Be(expected);
}
