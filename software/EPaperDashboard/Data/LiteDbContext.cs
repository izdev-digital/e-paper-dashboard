using LiteDB;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data;

public class LiteDbContext(string dbPath)
{
    private readonly LiteDatabase _db = new(dbPath);
    public ILiteCollection<User> Users => _db.GetCollection<User>("users");
    public ILiteCollection<Dashboard> Dashboards => _db.GetCollection<Dashboard>("dashboards");
}
