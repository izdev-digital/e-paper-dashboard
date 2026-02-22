using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbDeviceRepository(LiteDbContext context) : IDeviceRepository
{
    public Maybe<Device> FindById(DeviceId id) =>
        context.Devices.FindById(new ObjectId(id.Value));

    public Maybe<Device> FindByIdentifier(string deviceIdentifier) =>
        context.Devices.FindOne(d => d.DeviceIdentifier == deviceIdentifier);

    public List<Device> FindByDashboardId(DashboardId dashboardId) =>
        context.Devices.Find(d => d.DashboardId == dashboardId).ToList();

    public void Insert(Device device) =>
        context.Devices.Insert(device);

    public void Update(Device device) =>
        context.Devices.Update(device);

    public void Delete(DeviceId id) =>
        context.Devices.Delete(new ObjectId(id.Value));
}
