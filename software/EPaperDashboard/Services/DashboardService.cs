using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services;

public sealed class DashboardService(IDashboardRepository dashboardRepository)
{
    public List<Dashboard> GetDashboardsForUser(Guid userId) =>
        dashboardRepository.FindByUserId(userId);

    public void AddDashboard(Dashboard dashboard) =>
        dashboardRepository.Insert(dashboard);

    public void UpdateDashboard(Dashboard dashboard) =>
        dashboardRepository.Update(dashboard);

    public void DeleteDashboard(Guid dashboardId) =>
        dashboardRepository.Delete(dashboardId);

    public Maybe<Dashboard> GetDashboardByApiKey(string apiKey) =>
        dashboardRepository.FindByApiKey(apiKey);

    public Maybe<Dashboard> GetDashboardById(Guid dashboardId) =>
        dashboardRepository.FindById(dashboardId);

    public IEnumerable<Dashboard> GetAllDashboards() =>
        dashboardRepository.GetAll();
}
