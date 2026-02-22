using System.Text.Json.Serialization;

namespace EPaperDashboard.Models;

[JsonConverter(typeof(UserIdJsonConverter))]
public readonly record struct UserId(string Value) : ITypedId<UserId>
{
    public static UserId Empty => new(string.Empty);

    public static UserId New() => new(ObjectIdHelper.GenerateNew());

    public static UserId Parse(string value) => new(value);

    public static bool TryParse(string? value, out UserId result)
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

public sealed class UserIdJsonConverter : TypedIdJsonConverter<UserId> { }
