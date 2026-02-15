using EPaperDashboard.Guards;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Rendering;

public sealed class HassAuthStrategy(HassTokens hassTokens) : IAuthrorizationStrategy
{
    private readonly HassTokens _hassTokens = Guard.NotNull(hassTokens);

    public async Task AuthorizeAsync(DashboardPage page)
	{
		Guard.NotNull(page);
		// Navigate to HA root first to set localStorage on the correct origin
		await page.NavigateToRootAsync();
		await page.SetToken(_hassTokens);
		// Now navigate to the actual dashboard path — HA will find the token and authenticate
		await page.EnsureNavigatedAsync();
	}
}