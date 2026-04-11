using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides calendar event data for the calendar widget.
/// </summary>
public interface ICalendarDataProvider
{
    Task<Result<List<CalendarEvent>, string>> FetchCalendarEventsAsync(string dashboardId, string entityId, int durationHours = 168);

    /// <summary>
    /// Discovers all available calendar entities and fetches events for each.
    /// </summary>
    Task<Result<Dictionary<string, List<CalendarEvent>>, string>> FetchAllCalendarEventsAsync(string dashboardId, int durationHours = 168);
}
