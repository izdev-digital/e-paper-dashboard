using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace EPaperDashboard.Services.Rendering;

public sealed class BlackRedWhiteBinaryEncoder : ImageEncoder
{
	protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
	{
		var imagePixelCount = image.Width * image.Height;
		if (imagePixelCount % 8 != 0)
		{
			throw new ArgumentException("The number of image pixels should be dividable by 8");
		}

		byte blackByte = 0xFF;
		byte redByte = 0xFF;
		
		for (var currentPixel = 0; currentPixel < imagePixelCount;)
		{
			for (var bitCount = 0; bitCount < 8 && currentPixel < imagePixelCount; bitCount++, currentPixel++)
			{
				var x = currentPixel / image.Height;
				var y = currentPixel % image.Height;
				var resetBits = (byte)~(0x01 << (7 - (currentPixel % 8)));
				if (image[x, y].Equals(Color.Black.ToPixel<TPixel>()))
				{
					blackByte &= resetBits;
				}

				if (image[x, y].Equals(Color.Red.ToPixel<TPixel>()))
				{
					redByte &= resetBits;
				}
			}

			stream.WriteByte(blackByte);
			stream.WriteByte(redByte);
			blackByte = 0xFF;
			redByte = 0xFF;
		}
	}
}