using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbDashboardRepository(LiteDbContext context) : IDashboardRepository
{
    public Maybe<Dashboard> FindById(Guid id) =>
        context.Dashboards.FindById(id);

    public Maybe<Dashboard> FindByApiKey(string apiKey) =>
        context.Dashboards.FindOne(d => d.ApiKey == apiKey);

    public List<Dashboard> FindByUserId(Guid userId) =>
        context.Dashboards.Find(d => d.UserId == userId).ToList();

    public IEnumerable<Dashboard> GetAll() =>
        context.Dashboards.FindAll();

    public void Insert(Dashboard dashboard) =>
        context.Dashboards.Insert(dashboard);

    public void Update(Dashboard dashboard) =>
        context.Dashboards.Update(dashboard);

    public void Delete(Guid id) =>
        context.Dashboards.Delete(id);

    public void DeleteByUserId(Guid userId) =>
        context.Dashboards.DeleteMany(d => d.UserId == userId);
}
