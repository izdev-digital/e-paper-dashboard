namespace EPaperDashboard.Services.Rendering;

public readonly record struct Health(bool IsRendererAvailable, bool IsDashboardAvailable);
