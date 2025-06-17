using EPaperDashboard.Models;
using LiteDB;
using System.Collections.Generic;
using System.Linq;

namespace EPaperDashboard.Data;

public class DashboardService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public List<Dashboard> GetDashboardsForUser(ObjectId userId)
        => _dbContext.Dashboards.Find(d => d.UserId == userId).ToList();

    public void AddDashboard(Dashboard dashboard)
    {
        _dbContext.Dashboards.Insert(dashboard);
    }

    public void UpdateDashboard(Dashboard dashboard)
    {
        _dbContext.Dashboards.Update(dashboard);
    }

    public void DeleteDashboard(ObjectId dashboardId)
    {
        _dbContext.Dashboards.Delete(dashboardId);
    }
}
