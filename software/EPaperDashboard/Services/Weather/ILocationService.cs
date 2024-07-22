using EPaperDashboard.Models.Weather;
using FluentResults;

namespace EPaperDashboard.Services.Weather;

public interface ILocationService
{
    Task<Result<Location>> GetLocationDetailsAsync(string location);
}
