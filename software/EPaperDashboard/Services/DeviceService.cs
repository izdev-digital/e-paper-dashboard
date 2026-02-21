using CSharpFunctionalExtensions;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Services;

public sealed class DeviceService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public List<Device> GetDevicesForDashboard(ObjectId dashboardId) => _dbContext
        .Devices.Find(d => d.DashboardId == dashboardId).ToList();

    public void AddDevice(Device device) => _dbContext
        .Devices.Insert(device);

    public void UpdateDevice(Device device) => _dbContext
        .Devices.Update(device);

    public void DeleteDevice(ObjectId deviceId) => _dbContext
        .Devices.Delete(deviceId);

    public Maybe<Device> GetDeviceById(ObjectId deviceId) => _dbContext
        .Devices.FindById(deviceId);

    public Maybe<Device> GetDeviceByIdentifier(string deviceIdentifier) => _dbContext
        .Devices.FindOne(d => d.DeviceIdentifier == deviceIdentifier);
}
