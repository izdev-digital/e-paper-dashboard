using LiteDB;
using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using SystemTextJson = System.Text.Json;

namespace EPaperDashboard.Data;

public sealed class LiteDbContext
{
    private readonly LiteDatabase _db;

    public LiteDbContext()
    {
        var mapper = new BsonMapper();
        
        // Register custom serialization for JsonElement FIRST (before entity configuration)
        mapper.RegisterType(
            serialize: (jsonElement) => JsonElementToBsonValue(jsonElement),
            deserialize: (bsonValue) => BsonValueToJsonElement(bsonValue)
        );
        
        _db = new(Path.Combine(EnvironmentConfiguration.ConfigDir, "epaperdashboard.db"), mapper);
    }

    public ILiteCollection<User> Users => _db.GetCollection<User>("users");
    public ILiteCollection<Dashboard> Dashboards => _db.GetCollection<Dashboard>("dashboards");

    private static BsonValue JsonElementToBsonValue(SystemTextJson.JsonElement element)
    {
        return element.ValueKind switch
        {
            SystemTextJson.JsonValueKind.Object => JsonElementToBsonDocument(element),
            SystemTextJson.JsonValueKind.Array => JsonElementToBsonArray(element),
            SystemTextJson.JsonValueKind.String => new BsonValue(element.GetString()),
            SystemTextJson.JsonValueKind.Number => element.TryGetInt32(out var i) ? new BsonValue(i) :
                                    element.TryGetInt64(out var l) ? new BsonValue(l) :
                                    new BsonValue(element.GetDouble()),
            SystemTextJson.JsonValueKind.True => new BsonValue(true),
            SystemTextJson.JsonValueKind.False => new BsonValue(false),
            SystemTextJson.JsonValueKind.Null => BsonValue.Null,
            SystemTextJson.JsonValueKind.Undefined => new BsonDocument(), // Uninitialized JsonElement → empty object
            _ => BsonValue.Null
        };
    }

    private static BsonDocument JsonElementToBsonDocument(SystemTextJson.JsonElement element)
    {
        var doc = new BsonDocument();
        foreach (var property in element.EnumerateObject())
        {
            doc[property.Name] = JsonElementToBsonValue(property.Value);
        }
        return doc;
    }

    private static BsonArray JsonElementToBsonArray(SystemTextJson.JsonElement element)
    {
        var array = new BsonArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(JsonElementToBsonValue(item));
        }
        return array;
    }

    private static SystemTextJson.JsonElement BsonValueToJsonElement(BsonValue value)
    {
        var jsonString = BsonValueToJsonString(value);
        using var doc = SystemTextJson.JsonDocument.Parse(jsonString);
        return doc.RootElement.Clone();
    }

    private static string BsonValueToJsonString(BsonValue value)
    {
        if (value.IsNull) return "null";
        if (value.IsDocument) return value.AsDocument.ToString();
        if (value.IsArray) return value.AsArray.ToString();
        if (value.IsString) return SystemTextJson.JsonSerializer.Serialize(value.AsString);
        if (value.IsInt32) return value.AsInt32.ToString();
        if (value.IsInt64) return value.AsInt64.ToString();
        if (value.IsDouble) return value.AsDouble.ToString();
        if (value.IsBoolean) return value.AsBoolean.ToString().ToLower();
        if (value.IsDateTime) return SystemTextJson.JsonSerializer.Serialize(value.AsDateTime);
        return SystemTextJson.JsonSerializer.Serialize(value.ToString());
    }
}
