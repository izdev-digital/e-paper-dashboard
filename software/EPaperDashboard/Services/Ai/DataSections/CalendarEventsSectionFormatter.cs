using System.Text;

namespace EPaperDashboard.Services.Ai.DataSections;

public sealed class CalendarEventsSectionFormatter : IAiDataSectionFormatter
{
    public bool HasData(AiDataSnapshot data) => data.CalendarEvents.Count > 0;

    public string FormatSection(AiDataSnapshot data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Calendar Events");
        foreach (var (entityId, events) in data.CalendarEvents)
        {
            sb.AppendLine($"Calendar: {entityId} ({events.Count} events)");
            foreach (var evt in events.Take(10))
            {
                var time = evt.AllDay ? "All day" : evt.Start;
                sb.AppendLine($"  - {time}: {evt.Summary}");
            }
        }
        return sb.ToString();
    }
}
