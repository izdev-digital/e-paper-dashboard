using System.Text;

namespace EPaperDashboard.Services.Ai.DataSections;

public sealed class RssFeedSectionFormatter : IAiDataSectionFormatter
{
    public bool HasData(AiDataSnapshot data) => data.RssFeedEntries.Count > 0;

    public string FormatSection(AiDataSnapshot data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### RSS Feeds");
        foreach (var (entityId, entries) in data.RssFeedEntries)
        {
            sb.AppendLine($"Feed: {entityId} ({entries.Count} entries)");
            foreach (var entry in entries.Take(5))
            {
                sb.AppendLine($"  - {entry.Title}");
            }
        }
        return sb.ToString();
    }
}
