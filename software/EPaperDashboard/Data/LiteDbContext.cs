using LiteDB;
using EPaperDashboard.Models;
using EPaperDashboard.Utilities;

namespace EPaperDashboard.Data;

public sealed class LiteDbContext
{
    private readonly LiteDatabase _db = new(Path.Combine(EnvironmentConfiguration.ConfigDir, "epaperdashboard.db"));

    public ILiteCollection<User> Users => _db.GetCollection<User>("users");

    public ILiteCollection<Dashboard> Dashboards => _db.GetCollection<Dashboard>("dashboards");
}
