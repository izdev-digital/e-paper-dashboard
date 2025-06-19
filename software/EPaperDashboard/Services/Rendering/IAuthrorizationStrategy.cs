namespace EPaperDashboard.Services.Rendering;

public interface IAuthrorizationStrategy
{
	Task AuthorizeAsync(DashboardPage page);
}
