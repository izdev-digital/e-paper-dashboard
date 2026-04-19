using System.Text;

namespace EPaperDashboard.Services.Ai.DataSections;

public sealed class TodoItemsSectionFormatter : IAiDataSectionFormatter
{
    public bool HasData(AiDataSnapshot data) => data.TodoItems.Count > 0;

    public string FormatSection(AiDataSnapshot data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Todo Lists");
        foreach (var (entityId, items) in data.TodoItems)
        {
            sb.AppendLine($"Todo: {entityId} ({items.Count} items)");
            foreach (var item in items.Take(10))
            {
                sb.AppendLine($"  - [{item.Status}] {item.Summary}");
            }
        }
        return sb.ToString();
    }
}
