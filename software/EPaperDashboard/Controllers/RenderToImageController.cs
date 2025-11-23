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
	DashboardService dashboardService,
	ILogger<RenderToImageController> logger) : ControllerBase
{
	private readonly IPageToImageRenderingService _renderingService = renderingService;
	private readonly Func<HassTokens, HassAuthStrategy> _authStrategyFactory = token => new HassAuthStrategy(token);
	private readonly DashboardService _dashboardService = dashboardService;
	private readonly ILogger<RenderToImageController> _logger = logger;

	[HttpGet("binary")]
	public async Task<IActionResult> GetAsBinary(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] bool shouldDither = false)
	{
		try
		{
			_logger.LogInformation("GetAsBinary request received with size {Width}x{Height}, dither={ShouldDither}", imageSize.Width, imageSize.Height, shouldDither);
			
			var dashboardInfo = _dashboardService.GetDashboardByApiKey(apiKey).Bind(GetDashboardInfo);
			if (dashboardInfo.HasNoValue)
			{
				_logger.LogError("Dashboard not found for provided API key");
				return NotFound();
			}

			var (contentType, encoder) = GetEncoder("bin");
			var authStrategy = _authStrategyFactory(dashboardInfo.Value.Tokens);
			var result = await _renderingService
				.RenderDashboardAsync(dashboardInfo.Value.DashboardUri, imageSize, authStrategy)
				.Map(image => image
					.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither))
					.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal))
				.Match(async image => await ConvertToResult(image, encoder, contentType), ConvertToError);
			
			_logger.LogInformation("GetAsBinary request completed successfully");
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing GetAsBinary request with size {Width}x{Height}", imageSize.Width, imageSize.Height);
			throw;
		}
	}

	[HttpGet("converted")]
	public async Task<IActionResult> GetAsConvertedsImage(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] string format = "jpeg",
		[FromQuery] bool shouldDither = false)
	{
		try
		{
			_logger.LogInformation("GetAsConvertedsImage request received with size {Width}x{Height}, format={Format}, dither={ShouldDither}", imageSize.Width, imageSize.Height, format, shouldDither);
			
			var dashboardInfo = _dashboardService.GetDashboardByApiKey(apiKey).Bind(GetDashboardInfo);
			if (dashboardInfo.HasNoValue)
			{
				_logger.LogError("Dashboard not found for provided API key");
				return NotFound();
			}

			var (contentType, encoder) = GetEncoder(format);
			var authStrategy = _authStrategyFactory(dashboardInfo.Value.Tokens);
			var result = await _renderingService
				.RenderDashboardAsync(dashboardInfo.Value.DashboardUri, imageSize, authStrategy)
				.Map(image => image.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither)))
				.Match(async image => await ConvertToResult(image, encoder, contentType), ConvertToError);
			
			_logger.LogInformation("GetAsConvertedsImage request completed successfully");
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing GetAsConvertedsImage request with size {Width}x{Height}, format={Format}", imageSize.Width, imageSize.Height, format);
			throw;
		}
	}

	[HttpGet("original")]
	public async Task<IActionResult> GetAsImage(
		[Required][FromQuery] Size imageSize,
		[FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey,
		[FromQuery] string format = "jpeg")
	{
		try
		{
			_logger.LogInformation("GetAsImage request received with size {Width}x{Height}, format={Format}", imageSize.Width, imageSize.Height, format);
			
			var dashboardInfo = _dashboardService.GetDashboardByApiKey(apiKey).Bind(GetDashboardInfo);
			if (dashboardInfo.HasNoValue)
			{
				_logger.LogError("Dashboard not found for provided API key");
				return NotFound();
			}

			var (contentType, encoder) = GetEncoder(format);
			var authStrategy = _authStrategyFactory(dashboardInfo.Value.Tokens);
			var result = await _renderingService
				.RenderDashboardAsync(dashboardInfo.Value.DashboardUri, imageSize, authStrategy)
				.Match(async image => await ConvertToResult(image, encoder, contentType), ConvertToError);
			
			_logger.LogInformation("GetAsImage request completed successfully");
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing GetAsImage request with size {Width}x{Height}, format={Format}", imageSize.Width, imageSize.Height, format);
			throw;
		}
	}

	[HttpGet("health")]
	public async Task<IActionResult> GetHealth([FromHeader(Name = HttpHeaderNames.ApiKeyHeaderName)] string apiKey)
	{
		try
		{
			_logger.LogInformation("GetHealth request received");
			
			var result = await _dashboardService
				.GetDashboardByApiKey(apiKey)
				.Bind(GetDashboardUri)
				.Match(
					Some: async (dashboardUri, _) => (IActionResult)Ok(await _renderingService.GetHealth(dashboardUri)),
					None: _ =>
					{
						_logger.LogError("Dashboard not found for provided API key in health check");
						return Task.FromResult<IActionResult>(NotFound());
					}
				);
			
			_logger.LogInformation("GetHealth request completed successfully");
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing GetHealth request");
			throw;
		}
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
		return (
			DashboardUri: new Uri(hostUri, pathUri),
			Tokens: new HassTokens(dashboard.AccessToken, "Bearer", hassUrl, clientId)
		); ;
	}

	private static Maybe<Uri> GetDashboardUri(Dashboard dashboard) =>
		Uri.TryCreate(dashboard.Host, UriKind.Absolute, out var hostUri) &&
		Uri.TryCreate(dashboard.Path, UriKind.Relative, out var pathUri)
		? new Uri(hostUri, pathUri)
		: Maybe.None;

	private static IDither? GetDither(bool shouldDither) => shouldDither ? KnownDitherings.JarvisJudiceNinke : null;

	private async Task<IActionResult> ConvertToResult(IImage image, IImageEncoder encoder, string contentType)
	{
		var outStream = new MemoryStream();
		await image.SaveAsync(outStream, encoder);
		outStream.Seek(0, SeekOrigin.Begin);
		return File(outStream, contentType);
	}

	private Task<IActionResult> ConvertToError(string error) => Task.FromResult<IActionResult>(BadRequest(error));

	private static (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
	{
		"jpeg" => ("image/jpeg", new JpegEncoder()),
		"bmp" => ("image/bmp", new BmpEncoder()),
		"png" => ("image/png", new PngEncoder()),
		"bin" => ("application/octet-stream", new BlackRedWhiteBinaryEncoder()),
		_ => throw new NotSupportedException($"Format is not supported: {format}")
	};
}
