namespace EPaperDashboard.Services.Ai;

public interface IAiDataSectionFormatter
{
    bool HasData(AiDataSnapshot data);
    string FormatSection(AiDataSnapshot data);
}
