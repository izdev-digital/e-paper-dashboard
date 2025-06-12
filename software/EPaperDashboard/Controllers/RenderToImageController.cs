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

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/render")]
public sealed class RenderToImageController(IPageToImageRenderingService renderingService) : ControllerBase
{
	private readonly IPageToImageRenderingService _renderingService = renderingService;

	[HttpGet("binary")]
	public async Task<IActionResult> GetAsBinary(
		[Required][FromQuery] Size imageSize,
		[FromQuery] bool shouldDither = false)
	{
		return await _renderingService
			.RenderDashboardAsync(imageSize)
			.Map(image => image
				.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither))
				.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal))
			.Match(
				async image => await ConvertToResult(image, new BlackRedWhiteBinaryEncoder(), "application/octet-stream"),
				ConvertToError);
	}

	[HttpGet("converted")]
	public async Task<IActionResult> GetAsConvertedsImage(
		[Required][FromQuery] Size imageSize,
		[FromQuery] string format = "jpeg",
		[FromQuery] bool shouldDither = false)
	{
		var (contentType, encoder) = GetEncoder(format);
		return await _renderingService
			.RenderDashboardAsync(imageSize)
			.Map(image => image.Quantize(Palettes.RedBlackWhite, GetDither(shouldDither)))
			.Match(async image => await ConvertToResult(image, encoder, contentType), ConvertToError);
	}

	[HttpGet("original")]
	public async Task<IActionResult> GetAsImage(
		[Required][FromQuery] Size imageSize,
		[FromQuery] string format = "jpeg")
	{
		var (contentType, encoder) = GetEncoder(format);
		return await _renderingService
			.RenderDashboardAsync(imageSize)
			.Match(async image => await ConvertToResult(image, encoder, contentType), ConvertToError);
	}

	[HttpGet("health")]
	public async Task<IActionResult> GetHealth() => Ok(await _renderingService.GetHealth());

	private static IDither? GetDither(bool shouldDither) => shouldDither ? KnownDitherings.JarvisJudiceNinke : null;

	private async Task<IActionResult> ConvertToResult(IImage image, IImageEncoder encoder, string contentType)
	{
		var outStream = new MemoryStream();
		await image.SaveAsync(outStream, encoder);
		outStream.Seek(0, SeekOrigin.Begin);
		return File(outStream, contentType);
	}

	private Task<IActionResult> ConvertToError(string error) => Task.FromResult<IActionResult>(BadRequest(error));

	private (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
	{
		"jpeg" => ("image/jpeg", new JpegEncoder()),
		"bmp" => ("image/bmp", new BmpEncoder()),
		"png" => ("image/png", new PngEncoder()),
		_ => throw new NotSupportedException($"Format is not supported: {format}")
	};
}
