using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(UserLlmConfigIdJsonConverter))]
public readonly record struct UserLlmConfigId(string Value) : ITypedId<UserLlmConfigId>
{
    public static UserLlmConfigId Empty => new(string.Empty);

    public static UserLlmConfigId New() => new(ObjectIdHelper.GenerateNew());

    public static UserLlmConfigId Parse(string value) => new(value);

    public static bool TryParse(string? value, out UserLlmConfigId result)
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

public sealed class UserLlmConfigIdJsonConverter : TypedIdJsonConverter<UserLlmConfigId> { }
