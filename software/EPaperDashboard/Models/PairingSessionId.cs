using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(PairingSessionIdJsonConverter))]
public readonly record struct PairingSessionId(string Value) : ITypedId<PairingSessionId>
{
    public static PairingSessionId Empty => new(string.Empty);

    public static PairingSessionId New() => new(ObjectIdHelper.GenerateNew());

    public static PairingSessionId Parse(string value) => new(value);

    public static bool TryParse(string? value, out PairingSessionId result)
    {
        if (ObjectIdHelper.IsValid(value))
        {
            result = new(value!);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => Value;
}

public sealed class PairingSessionIdJsonConverter : TypedIdJsonConverter<PairingSessionId> { }
