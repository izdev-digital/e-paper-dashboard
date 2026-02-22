using LiteDB;
using EPaperDashboard.Models;
using EPaperDashboard.Utilities;
using SystemTextJson = System.Text.Json;

namespace EPaperDashboard.Data.LiteDb;

/// <summary>
/// Internal LiteDB connection context. Only used by LiteDB repository implementations.
/// Business logic must never take a direct dependency on this class.
/// </summary>
internal sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _db;

    public LiteDbContext()
    {
        var mapper = new BsonMapper();

        // Register strongly-typed ID types — stored as ObjectId values in the database
        mapper.RegisterType<UserId>(
            serialize: id => new BsonValue(new ObjectId(id.Value)),
            deserialize: bson => new UserId(bson.AsObjectId.ToString()));
        mapper.RegisterType<DashboardId>(
            serialize: id => new BsonValue(new ObjectId(id.Value)),
            deserialize: bson => new DashboardId(bson.AsObjectId.ToString()));
        mapper.RegisterType<DeviceId>(
            serialize: id => new BsonValue(new ObjectId(id.Value)),
            deserialize: bson => new DeviceId(bson.AsObjectId.ToString()));
        mapper.RegisterType<PairingSessionId>(
            serialize: id => new BsonValue(new ObjectId(id.Value)),
            deserialize: bson => new PairingSessionId(bson.AsObjectId.ToString()));

        // Register custom serialization for JsonElement (must be after ID types)
        mapper.RegisterType(
            serialize: (jsonElement) => JsonElementToBsonValue(jsonElement),
            deserialize: (bsonValue) => BsonValueToJsonElement(bsonValue)
        );

        var connectionString = new ConnectionString
        {
            Filename = Path.Combine(EnvironmentConfiguration.ConfigDir, "epaperdashboard.db"),
            Connection = ConnectionType.Direct
        };

        _db = new(connectionString, mapper);
        _db.Checkpoint();
    }

    internal ILiteCollection<User> Users => _db.GetCollection<User>("users");
    internal ILiteCollection<Dashboard> Dashboards => _db.GetCollection<Dashboard>("dashboards");
    internal ILiteCollection<Device> Devices => _db.GetCollection<Device>("devices");
    internal ILiteCollection<PairingSession> PairingSessions => _db.GetCollection<PairingSession>("pairingSessions");

    public void Dispose() => _db.Dispose();

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
            SystemTextJson.JsonValueKind.Undefined => new BsonDocument(),
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
