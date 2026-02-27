using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides entity state data for widgets that display sensor/entity values
/// (e.g., header badges, weather current state, app-icon).
/// </summary>
public interface IEntityStateProvider
{
    Task<Result<List<HassEntityState>, string>> FetchEntityStatesAsync(string dashboardId, string[] entityIds);
}
