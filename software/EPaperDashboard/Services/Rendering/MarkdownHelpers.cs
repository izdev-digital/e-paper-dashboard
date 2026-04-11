using System.Text.RegularExpressions;

namespace EPaperDashboard.Services.Rendering;

public static class MarkdownHelpers
{
    private static readonly Regex HorizontalRulePattern = new(@"^[-*_]{3,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TaskListPattern = new(@"^[-*+]\s\[[ xX]\]\s", RegexOptions.Compiled);
    private static readonly Regex TaskCheckedPattern = new(@"^[-*+]\s\[[xX]\]", RegexOptions.Compiled);
    private static readonly Regex IndentedSubListPattern = new(@"^\s{2,}[-*+]\s", RegexOptions.Compiled);
    private static readonly Regex NumberedListPattern = new(@"^\d+\.\s", RegexOptions.Compiled);
    private static readonly Regex NumberedListCapture = new(@"^(\d+\.)\s(.*)$", RegexOptions.Compiled);

    private static readonly Regex ImagePattern = new(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex LinkPattern = new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex BoldItalic3Star = new(@"\*{3}(.+?)\*{3}", RegexOptions.Compiled);
    private static readonly Regex BoldItalic3Under = new(@"_{3}(.+?)_{3}", RegexOptions.Compiled);
    private static readonly Regex Bold2Star = new(@"\*{2}(.+?)\*{2}", RegexOptions.Compiled);
    private static readonly Regex Bold2Under = new(@"_{2}(.+?)_{2}", RegexOptions.Compiled);
    private static readonly Regex ItalicStar = new(@"\*(.+?)\*", RegexOptions.Compiled);
    private static readonly Regex ItalicUnder = new(@"(?<=\s|^)_(.+?)_(?=\s|$)", RegexOptions.Compiled);
    private static readonly Regex Strikethrough = new(@"~~(.+?)~~", RegexOptions.Compiled);
    private static readonly Regex InlineCode = new(@"`(.+?)`", RegexOptions.Compiled);

    public static bool IsHorizontalRule(string line) => HorizontalRulePattern.IsMatch(line);
    public static bool IsTaskListItem(string line) => TaskListPattern.IsMatch(line);
    public static bool IsTaskCheckedItem(string line) => TaskCheckedPattern.IsMatch(line);
    public static bool IsIndentedSubList(string line) => IndentedSubListPattern.IsMatch(line);
    public static bool IsNumberedList(string line) => NumberedListPattern.IsMatch(line);
    public static Match MatchNumberedList(string line) => NumberedListCapture.Match(line);

    public static string StripInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = ImagePattern.Replace(text, "$1");
        text = LinkPattern.Replace(text, "$1");
        text = BoldItalic3Star.Replace(text, "$1");
        text = BoldItalic3Under.Replace(text, "$1");
        text = Bold2Star.Replace(text, "$1");
        text = Bold2Under.Replace(text, "$1");
        text = ItalicStar.Replace(text, "$1");
        text = ItalicUnder.Replace(text, "$1");
        text = Strikethrough.Replace(text, "$1");
        text = InlineCode.Replace(text, "$1");

        return text;
    }

    public static bool IsEntirelyBold(string line)
    {
        var trimmed = line.Trim();
        return (trimmed.StartsWith("**") && trimmed.EndsWith("**") && trimmed.Length > 4)
            || (trimmed.StartsWith("__") && trimmed.EndsWith("__") && trimmed.Length > 4);
    }
}
