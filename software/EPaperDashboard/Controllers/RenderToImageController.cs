using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenQA.Selenium.Chrome;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using FluentResults;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("[controller]")]
public class RenderToImageController : ControllerBase
{
    [HttpGet]
    [Route("text")]
    public async Task<IActionResult> GetAsText([FromQuery] ImageSizeDto imageSize)
    {
        try
        {
            var imageResult = await GetImageAsync(imageSize);
            if (imageResult.IsFailed)
            {
                return NoContent();
            }

            var image = imageResult.Value;
            image.Mutate(x => x.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));
            var cloneImage = image.Clone(x => x.ProcessPixelRowsAsVector4(row =>
            {
                for (int index = 0; index < row.Length; index++)
                {
                    var pixel = row[index];

                    row[index] = pixel.X >= 0.9 &&
                        Math.Abs(pixel.Y - pixel.Z) <= 0.1 &&
                        pixel.Y >= 0.1 && pixel.Z >= 0.1
                            ? new Vector4(1, 0, 0, 0)
                            : new Vector4(1, 1, 1, 1);
                }
            }));
            image.Mutate(x =>
            {
                // x.BinaryDither(KnownDitherings.Atkinson, Color.Black, Color.White);
                x.BinaryThreshold(0.9f);
            });

            var outStream = new MemoryStream();
            image.Save(outStream, new BinaryEncoder(x => x.R < 200));
            cloneImage.Save(outStream, new BinaryEncoder(x => x.R > 200 && x.G > 200 && x.B > 200));
            outStream.Seek(0, SeekOrigin.Begin);
            return File(outStream, "text/plain");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }

        return NoContent();
    }

    private static async Task<Result<Image>> GetImageAsync(ImageSizeDto imageSize)
    {
        var options = new ChromeOptions();
        options.AddArguments(
            "--headless=new",
            "--disable-gpu",
            "--hide-scrollbars",
            $"--window-size={imageSize.Width},{imageSize.Height}"
        );

        var driver = new ChromeDriver(options);
        try
        {
            await driver.Navigate().GoToUrlAsync("https://localhost:7297/Dashboard");
            var screenshot = driver.GetScreenshot();
            var image = Image.Load(screenshot.AsByteArray);

            image.Mutate(x =>
            {
                x.Resize(imageSize.Width, imageSize.Height);
            });
            return image;
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
        finally
        {
            driver.Close();
        }
    }

    [HttpGet]
    [Route("image")]
    public async Task<IActionResult> GetAsImage([FromQuery] ImageSizeDto imageSize)
    {
        var imageResult = await GetImageAsync(imageSize);
        if (imageResult.IsFailed)
        {
            return NoContent();
        }

        var image = imageResult.Value;
        var outStream = new MemoryStream();
        image.Save(outStream, new JpegEncoder());
        outStream.Seek(0, SeekOrigin.Begin);
        return File(outStream, "image/jpg");
    }
}

public record ImageSizeDto(int Width, int Height);

public class BinaryEncoder : ImageEncoder
{
    private readonly Func<Rgba32, bool> isZero;

    public BinaryEncoder(Func<Rgba32, bool> isZero) => this.isZero = isZero;

    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        byte currentValue = 0xFF;
        var index = 0;
        var pixel = new Rgba32(1, 1, 1, 1);

        WriteEncoded("{\n");

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                image[x, y].ToRgba32(ref pixel);
                if (isZero(pixel))
                {
                    currentValue &= (byte)~(0x01 << (7 - index));
                }
                index = (index + 1) % 8;
                if (index == 0)
                {
                    WriteElement(currentValue);
                    // stream.WriteByte(currentValue);
                    currentValue = 0xFF;
                }
            }
            stream.Write(Encoding.UTF8.GetBytes("\n"));
        }
        if (index > 0)
        {
            WriteElement(currentValue);
            // stream.WriteByte(currentValue);
        }

        WriteEncoded("\n}");

        void WriteElement(byte value)
        {
            WriteEncoded($"0x{value:X2}, ");
        }

        void WriteEncoded(string value)
        {
            stream.Write(Encoding.UTF8.GetBytes(value));
        }
    }
}