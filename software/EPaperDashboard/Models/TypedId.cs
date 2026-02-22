using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

internal static class ObjectIdHelper
{
    public static string GenerateNew()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsValid(string? value) =>
        value is { Length: 24 } && value.All(Uri.IsHexDigit);
}

public interface ITypedId<TSelf> where TSelf : struct, ITypedId<TSelf>
{
    string Value { get; }
    static abstract TSelf Empty { get; }
    static abstract bool TryParse(string? value, out TSelf result);
}

public abstract class TypedIdJsonConverter<T> : JsonConverter<T>
    where T : struct, ITypedId<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        T.TryParse(reader.GetString(), out var id) ? id : T.Empty;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
