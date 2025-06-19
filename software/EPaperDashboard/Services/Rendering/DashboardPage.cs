using EPaperDashboard.Models;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace EPaperDashboard.Services.Rendering;

public sealed class DashboardPage(IPage page, Uri dashboardUri)
{
	private readonly IPage _page = page;
	private readonly Uri _dashboardUri = dashboardUri;

	public async Task EnsureNavigatedAsync()
	{
		await _page.GotoAsync(_dashboardUri.AbsoluteUri);
		await WaitAsync();
	}

	public async Task SetToken(HassTokens token)
	{
		var hassTokens = JsonConvert.SerializeObject(token);
		await _page.EvaluateAsync("token => { localStorage.setItem('hassTokens', token); }", hassTokens);
	}

	public async Task<byte[]> TakeScreenshotAsync() => await _page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Jpeg });

	private async Task WaitAsync()
	{
		await _page.WaitForLoadStateAsync(LoadState.Load);
		await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
		await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
	}
}