using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides historical entity state data for the graph widget.
/// </summary>
public interface IEntityHistoryProvider
{
    Task<Result<Dictionary<string, List<HistoryState>>, string>> FetchEntityHistoryAsync(string dashboardId, IEnumerable<string> entityIds, int hours = 24);
}
