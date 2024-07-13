using System.Runtime.CompilerServices;

namespace EPaperDashboard.Guards;

public static class Guard
{
    public static string NeitherNullNorWhitespace(string? value, string parameterName) =>
     string.IsNullOrWhiteSpace(value ?? throw new ArgumentNullException(parameterName))
     ? throw new ArgumentException("Argument is either empty or whitespace")
     : value;
}
