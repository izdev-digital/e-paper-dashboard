using Microsoft.AspNetCore.Mvc;
using EPaperDashboard.Services.Rendering;
using SixLabors.ImageSharp.Processing;
using System.ComponentModel.DataAnnotations;
using EPaperDashboard.Utilities;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Models;
using EPaperDashboard.Services;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/render")]
[Authorize(Policy = "ApiKeyPolicy")]
public sealed class RenderToImageController(
	IPageToImageRenderingService renderingService,
	DashboardService dashboardService) : ControllerBase
{
	[HttpGet("binary")]
	public async Task<IActionResult> GetAsBinary(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] bool shouldDither = false) =>
		await RenderImage(apiKey, imageSize, "bin", image => image
			.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither))
			.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));

	[HttpGet("converted")]
	public async Task<IActionResult> GetAsConvertedsImage(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] string format = "jpeg",
		[FromQuery] bool shouldDither = false) =>
		await RenderImage(apiKey, imageSize, format, image =>
			image.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither)));

	[HttpGet("original")]
	public async Task<IActionResult> GetAsImage(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] string format = "jpeg") =>
		await RenderImage(apiKey, imageSize, format);

	[HttpGet("health")]
	public async Task<IActionResult> GetHealth([FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey) =>
		await dashboardService
			.GetDashboardByApiKey(apiKey)
			.Bind(GetDashboardUri)
			.Match(
				Some: async (uri, _) => (IActionResult)Ok(await renderingService.GetHealth(uri)),
				None: _ => Task.FromResult<IActionResult>(NotFound()));

	private async Task<IActionResult> RenderImage(
		string apiKey,
		Size imageSize,
		string format,
		Func<IImage, IImage>? transform = null)
	{
		var dashboardInfo = dashboardService.GetDashboardByApiKey(apiKey).Bind(GetDashboardInfo);
		if (dashboardInfo.HasNoValue)
		{
			return NotFound();
		}

		var (contentType, encoder) = GetEncoder(format);
		var authStrategy = new HassAuthStrategy(dashboardInfo.Value.Tokens);

		return await renderingService
			.RenderDashboardAsync(dashboardInfo.Value.DashboardUri, imageSize, authStrategy)
			.Map(image => transform?.Invoke(image) ?? image)
			.Match(
				image => ConvertToResult(image, encoder, contentType),
				error => Task.FromResult<IActionResult>(BadRequest(error)));
	}

	private static Maybe<(Uri DashboardUri, HassTokens Tokens)> GetDashboardInfo(Dashboard dashboard)
	{
		if (string.IsNullOrWhiteSpace(dashboard.AccessToken)
			|| !Uri.TryCreate(dashboard.Host, UriKind.Absolute, out var hostUri)
			|| !Uri.TryCreate(dashboard.Path, UriKind.Relative, out var pathUri))
		{
			return Maybe.None;
		}

		var hassUrl = hostUri.AbsoluteUri.TrimEnd('/');
		var clientId = EnvironmentConfiguration.ClientUri.AbsoluteUri.TrimEnd('/');
		return (new Uri(hostUri, pathUri), new HassTokens(dashboard.AccessToken, "Bearer", hassUrl, clientId));
	}

	private static Maybe<Uri> GetDashboardUri(Dashboard dashboard) =>
		Uri.TryCreate(dashboard.Host, UriKind.Absolute, out var hostUri) &&
		Uri.TryCreate(dashboard.Path, UriKind.Relative, out var pathUri)
		? new Uri(hostUri, pathUri)
		: Maybe.None;

	private static IDither? GetDither(bool shouldDither) =>
		shouldDither ? KnownDitherings.JarvisJudiceNinke : null;

	private async Task<IActionResult> ConvertToResult(IImage image, IImageEncoder encoder, string contentType)
	{
		var outStream = new MemoryStream();
		await image.SaveAsync(outStream, encoder);
		outStream.Seek(0, SeekOrigin.Begin);
		return File(outStream, contentType);
	}

	private static (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
	{
		"jpeg" => ("image/jpeg", new JpegEncoder()),
		"bmp" => ("image/bmp", new BmpEncoder()),
		"png" => ("image/png", new PngEncoder()),
		"bin" => ("application/octet-stream", new BlackRedWhiteBinaryEncoder()),
		_ => throw new NotSupportedException($"Format is not supported: {format}")
	};
}
