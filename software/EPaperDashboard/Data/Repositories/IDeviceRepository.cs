using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IDeviceRepository
{
    Maybe<Device> FindById(DeviceId id);
    Maybe<Device> FindByIdentifier(string deviceIdentifier);
    List<Device> FindByDashboardId(DashboardId dashboardId);
    void Insert(Device device);
    void Update(Device device);
    void Delete(DeviceId id);
}
