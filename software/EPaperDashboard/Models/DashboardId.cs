using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(DashboardIdJsonConverter))]
public readonly record struct DashboardId(string Value) : ITypedId<DashboardId>
{
    public static DashboardId Empty => new(string.Empty);

    public static DashboardId New() => new(ObjectIdHelper.GenerateNew());

    public static DashboardId Parse(string value) => new(value);

    public static bool TryParse(string? value, out DashboardId result)
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

public sealed class DashboardIdJsonConverter : TypedIdJsonConverter<DashboardId> { }
