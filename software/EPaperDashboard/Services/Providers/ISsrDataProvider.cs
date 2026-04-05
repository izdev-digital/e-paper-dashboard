using EPaperDashboard.Models.Rendering;
using UserId = EPaperDashboard.Models.UserId;

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
    /// Delegates to per-widget providers (entity states, todo, calendar, weather, RSS, history, AI text).
    /// </summary>
    /// <param name="dashboardId">The dashboard to fetch data for.</param>
    /// <param name="layout">The layout configuration.</param>
    /// <param name="userId">Optional user ID used to resolve the LLM provider for ai-text widgets.</param>
    Task<SsrData> FetchSsrDataAsync(string dashboardId, LayoutConfig layout, UserId userId = default);
}
