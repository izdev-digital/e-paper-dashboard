using System.Text;

namespace EPaperDashboard.Services.Ai.DataSections;

public sealed class EntityStateSectionFormatter : IAiDataSectionFormatter
{
    public bool HasData(AiDataSnapshot data) => data.EntityStates.Count > 0;

    public string FormatSection(AiDataSnapshot data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Entity States");
        foreach (var (entityId, state) in data.EntityStates)
        {
            var friendlyName = state.Attributes.TryGetValue("friendly_name", out var fn)
                ? fn?.ToString() : null;
            var unit = state.Attributes.TryGetValue("unit_of_measurement", out var u)
                ? u?.ToString() : null;

            var display = !string.IsNullOrEmpty(friendlyName) ? $"{friendlyName} ({entityId})" : entityId;
            var value = !string.IsNullOrEmpty(unit) ? $"{state.State} {unit}" : state.State;
            sb.AppendLine($"- {display}: {value}");
        }
        return sb.ToString();
    }
}
