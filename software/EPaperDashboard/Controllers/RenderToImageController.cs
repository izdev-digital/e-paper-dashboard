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

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/render")]
public class RenderToImageController(IPageToImageRenderingService renderingService) : ControllerBase
{
	private readonly IPageToImageRenderingService _renderingService = renderingService;

	[HttpGet]
	[Route("binary")]
	public async Task<IActionResult> GetAsBinary([Required][FromQuery] Size imageSize) =>
		await RenderPage(
			imageSize,
			image => image
				.Quantize(Palettes.RedBlackWhite)
				.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal),
			async (image, outStream) => await image.SaveAsync(outStream, new BlackRedWhiteBinaryEncoder()),
			"application/octet-stream");

	[HttpGet]
	[Route("converted")]
	public async Task<IActionResult> GetAsConvertedsImage([Required][FromQuery] Size imageSize, [Required][FromQuery] string format)
	{
		var (contentType, encoder) = GetEncoder(format);
		return await RenderPage(
			imageSize,
			image => image
				.Quantize(Palettes.RedBlackWhite),
			async (image, outStream) => await image.SaveAsync(outStream, encoder),
			contentType);
	}

	[HttpGet]
	[Route("original")]
	public async Task<IActionResult> GetAsImage([Required][FromQuery] Size imageSize, [Required][FromQuery] string format)
	{
		var (contentType, encoder) = GetEncoder(format);
		return await RenderPage(
			imageSize,
			image => image,
			async (image, outStream) => await image.SaveAsync(outStream, encoder),
			contentType);
	}

	private async Task<IActionResult> RenderPage(Size imageSize, Func<IImage, IImage> convert, Func<IImage, MemoryStream, Task> serialize, string contentType) =>
		await _renderingService
			.RenderPageAsync(imageSize)
			.Map(convert)
			.Match(
				onSuccess: async image =>
				{
					var outStream = new MemoryStream();
					await serialize(image, outStream);
					outStream.Seek(0, SeekOrigin.Begin);
					return (IActionResult)File(outStream, contentType);
				},
				onFailure: error => Task.FromResult<IActionResult>(Problem(error, statusCode: StatusCodes.Status400BadRequest)));

	private (string contentType, IImageEncoder encoder) GetEncoder(string format) => format switch
	{
		"jpeg" => ("image/jpeg", new JpegEncoder()),
		"bmp" => ("image/bmp", new BmpEncoder()),
		"png" => ("image/png", new PngEncoder()),
		_ => throw new NotSupportedException($"Format is not supported: {format}")
	};
}
