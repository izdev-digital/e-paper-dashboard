using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IDeviceRepository
{
    Maybe<Device> FindById(Guid id);
    Maybe<Device> FindByIdentifier(string deviceIdentifier);
    List<Device> FindByDashboardId(Guid dashboardId);
    void Insert(Device device);
    void Update(Device device);
    void Delete(Guid id);
}
