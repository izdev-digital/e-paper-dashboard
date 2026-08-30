namespace EPaperDashboard.Services.Rendering;

/// <summary>
/// Bounded, short-lived storage for designer PNG responses. Images are fetched by the browser as
/// binary data and remain scoped to the authenticated user and dashboard.
/// </summary>
public sealed class DesignerPreviewImageStore(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private const long MaxBytes = 64L * 1024 * 1024;
    private const int MaxEntries = 64;
    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _totalBytes;

    public string Add(string userId, string dashboardId, byte[] png)
    {
        var now = timeProvider.GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            Prune(now);
            while (_entries.Count >= MaxEntries || _totalBytes + png.LongLength > MaxBytes)
            {
                var oldest = _entries.MinBy(item => item.Value.CreatedAt);
                if (oldest.Key is null) break;
                Remove(oldest.Key);
            }

            if (png.LongLength > MaxBytes)
                throw new InvalidOperationException("Rendered designer preview exceeds the temporary image limit.");

            _entries[token] = new Entry(userId, dashboardId, png, now, now + Lifetime);
            _totalBytes += png.LongLength;
        }
        return token;
    }

    public bool TryGet(string token, string userId, string dashboardId, out byte[] png)
    {
        lock (_lock)
        {
            Prune(timeProvider.GetUtcNow());
            if (_entries.TryGetValue(token, out var entry)
                && entry.UserId == userId
                && entry.DashboardId == dashboardId)
            {
                png = entry.Png;
                return true;
            }
        }
        png = [];
        return false;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var token in _entries.Where(item => item.Value.ExpiresAt <= now)
                     .Select(item => item.Key).ToArray())
            Remove(token);
    }

    private void Remove(string token)
    {
        if (_entries.Remove(token, out var entry)) _totalBytes -= entry.Png.LongLength;
    }

    private sealed record Entry(
        string UserId,
        string DashboardId,
        byte[] Png,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt);
}
