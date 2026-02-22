using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbDeviceRepository(LiteDbContext context) : IDeviceRepository
{
    public Maybe<Device> FindById(Guid id) =>
        context.Devices.FindById(id);

    public Maybe<Device> FindByIdentifier(string deviceIdentifier) =>
        context.Devices.FindOne(d => d.DeviceIdentifier == deviceIdentifier);

    public List<Device> FindByDashboardId(Guid dashboardId) =>
        context.Devices.Find(d => d.DashboardId == dashboardId).ToList();

    public void Insert(Device device) =>
        context.Devices.Insert(device);

    public void Update(Device device) =>
        context.Devices.Update(device);

    public void Delete(Guid id) =>
        context.Devices.Delete(id);
}
