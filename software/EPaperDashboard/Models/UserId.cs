using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(UserIdJsonConverter))]
public readonly record struct UserId(string Value)
{
    public static readonly UserId Empty = new(string.Empty);

    public static UserId New()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    public static UserId Parse(string value) => new(value);

    public static bool TryParse(string? value, out UserId result)
    {
        if (IsValidObjectId(value))
        {
            result = new(value!);
            return true;
        }

        result = Empty;
        return false;
    }

    private static bool IsValidObjectId(string? value)
    {
        if (value is null || value.Length != 24) return false;
        foreach (var c in value)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }

    public override string ToString() => Value;
}

public sealed class UserIdJsonConverter : JsonConverter<UserId>
{
    public override UserId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UserId.TryParse(reader.GetString(), out var id) ? id : UserId.Empty;

    public override void Write(Utf8JsonWriter writer, UserId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
