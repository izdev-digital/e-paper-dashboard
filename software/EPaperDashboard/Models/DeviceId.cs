using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(DeviceIdJsonConverter))]
public readonly record struct DeviceId(string Value) : ITypedId<DeviceId>
{
    public static DeviceId Empty => new(string.Empty);

    public static DeviceId New() => new(ObjectIdHelper.GenerateNew());

    public static DeviceId Parse(string value) => new(value);

    public static bool TryParse(string? value, out DeviceId result)
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

public sealed class DeviceIdJsonConverter : TypedIdJsonConverter<DeviceId> { }
