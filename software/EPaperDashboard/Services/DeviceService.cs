using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services;

public sealed class DeviceService(IDeviceRepository deviceRepository)
{
    public List<Device> GetDevicesForDashboard(Guid dashboardId) =>
        deviceRepository.FindByDashboardId(dashboardId);

    public void AddDevice(Device device) =>
        deviceRepository.Insert(device);

    public void UpdateDevice(Device device) =>
        deviceRepository.Update(device);

    public void DeleteDevice(Guid deviceId) =>
        deviceRepository.Delete(deviceId);

    public Maybe<Device> GetDeviceById(Guid deviceId) =>
        deviceRepository.FindById(deviceId);

    public Maybe<Device> GetDeviceByIdentifier(string deviceIdentifier) =>
        deviceRepository.FindByIdentifier(deviceIdentifier);
}
