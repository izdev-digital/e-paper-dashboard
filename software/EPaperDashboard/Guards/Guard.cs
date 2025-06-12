using System.Runtime.CompilerServices;

namespace EPaperDashboard.Guards;

public static class Guard
{
    public static string NeitherNullNorWhitespace(string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null) =>
        string.IsNullOrWhiteSpace(value ?? throw new ArgumentNullException(parameterName))
            ? throw new ArgumentException("Argument is either empty or whitespace")
            : value;

    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null) =>
        value ?? throw new ArgumentNullException(parameterName);
}
