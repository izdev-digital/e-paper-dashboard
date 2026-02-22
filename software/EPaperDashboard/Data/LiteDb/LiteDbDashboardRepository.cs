using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbDashboardRepository(LiteDbContext context) : IDashboardRepository
{
    public Maybe<Dashboard> FindById(DashboardId id) =>
        context.Dashboards.FindById(new ObjectId(id.Value));

    public Maybe<Dashboard> FindByApiKey(string apiKey) =>
        context.Dashboards.FindOne(d => d.ApiKey == apiKey);

    public List<Dashboard> FindByUserId(UserId userId) =>
        context.Dashboards.Find(d => d.UserId == userId).ToList();

    public IEnumerable<Dashboard> GetAll() =>
        context.Dashboards.FindAll();

    public void Insert(Dashboard dashboard) =>
        context.Dashboards.Insert(dashboard);

    public void Update(Dashboard dashboard) =>
        context.Dashboards.Update(dashboard);

    public void Delete(DashboardId id) =>
        context.Dashboards.Delete(new ObjectId(id.Value));

    public void DeleteByUserId(UserId userId) =>
        context.Dashboards.DeleteMany(d => d.UserId == userId);
}
