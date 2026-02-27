using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides RSS feed entry data for the rss-feed widget.
/// </summary>
public interface IRssFeedDataProvider
{
    Task<Result<List<RssFeedEntry>, string>> FetchRssFeedEntriesAsync(string dashboardId, string entityId);
}
