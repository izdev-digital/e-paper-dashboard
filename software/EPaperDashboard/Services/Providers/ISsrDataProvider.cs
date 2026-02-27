using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Orchestrates data fetching for server-side rendering by coordinating
/// the per-widget data providers. Replaces direct Home Assistant calls
/// in the rendering pipeline.
/// </summary>
public interface ISsrDataProvider
{
    /// <summary>
    /// Fetches all data needed to render a dashboard layout.
    /// Delegates to per-widget providers (entity states, todo, calendar, weather, RSS, history).
    /// </summary>
    Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout);
}
