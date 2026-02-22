using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(PairingSessionIdJsonConverter))]
public readonly record struct PairingSessionId(string Value)
{
    public static readonly PairingSessionId Empty = new(string.Empty);

    public static PairingSessionId New()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    public static PairingSessionId Parse(string value) => new(value);

    public static bool TryParse(string? value, out PairingSessionId result)
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

public sealed class PairingSessionIdJsonConverter : JsonConverter<PairingSessionId>
{
    public override PairingSessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        PairingSessionId.TryParse(reader.GetString(), out var id) ? id : PairingSessionId.Empty;

    public override void Write(Utf8JsonWriter writer, PairingSessionId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
