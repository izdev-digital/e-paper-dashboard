using EPaperDashboard.Guards;
using EPaperDashboard.Models;

namespace EPaperDashboard.Services.Rendering;

public sealed class HassAuthStrategy(HassTokens hassTokens) : IAuthrorizationStrategy
{
    private readonly HassTokens _hassTokens = Guard.NotNull(hassTokens);

    public async Task AuthorizeAsync(DashboardPage page)
	{
		Guard.NotNull(page);
		await page.EnsureNavigatedAsync();
		await page.SetToken(_hassTokens);
		await page.ReloadAsync();
	}
}