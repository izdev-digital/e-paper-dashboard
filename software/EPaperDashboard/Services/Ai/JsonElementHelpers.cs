using System.Text.Json;

namespace EPaperDashboard.Services.Ai;

public static class JsonElementHelpers
{
    public static string? GetStringProp(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var p)
        && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    public static int? GetIntProp(JsonElement el, string prop) =>
        el.ValueKind == JsonValueKind.Object
        && el.TryGetProperty(prop, out var p)
        && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : null;

    public static Dictionary<string, object?> PatchJsonObject(JsonElement original, string key, string value)
    {
        var dict = new Dictionary<string, object?>();
        if (original.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in original.EnumerateObject())
            {
                if (prop.Name == key)
                {
                    continue;
                }
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.Clone()
                };
            }
        }
        dict[key] = value;
        return dict;
    }
}
