using CSharpFunctionalExtensions;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Services;

public sealed class DashboardService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public List<Dashboard> GetDashboardsForUser(ObjectId userId) => _dbContext
        .Dashboards.Find(d => d.UserId == userId).ToList();

    public void AddDashboard(Dashboard dashboard) => _dbContext
        .Dashboards.Insert(dashboard);

    public void UpdateDashboard(Dashboard dashboard) => _dbContext
        .Dashboards.Update(dashboard);

    public void DeleteDashboard(ObjectId dashboardId) => _dbContext
        .Dashboards.Delete(dashboardId);

    public Maybe<Dashboard> GetDashboardByApiKey(string apiKey) => _dbContext
        .Dashboards.FindOne(d => d.ApiKey == apiKey);

    public Maybe<Dashboard> GetDashboardById(ObjectId dashboardId) => _dbContext
        .Dashboards.FindById(dashboardId);

    public IEnumerable<Dashboard> GetAllDashboards() => _dbContext
        .Dashboards.FindAll();
}
