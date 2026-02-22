using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IDashboardRepository
{
    Maybe<Dashboard> FindById(Guid id);
    Maybe<Dashboard> FindByApiKey(string apiKey);
    List<Dashboard> FindByUserId(Guid userId);
    IEnumerable<Dashboard> GetAll();
    void Insert(Dashboard dashboard);
    void Update(Dashboard dashboard);
    void Delete(Guid id);
    void DeleteByUserId(Guid userId);
}
