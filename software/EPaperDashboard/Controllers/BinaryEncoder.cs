using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace EPaperDashboard.Controllers;

public sealed class BlackRedWhiteBinaryEncoder : ImageEncoder
{
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        byte currentValue = 0xFF;
        var index = 0;

        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                for (var colorId = 0; colorId < 2; colorId++)
                {
                    if (image[x, y].Equals(Color.Red.ToPixel<TPixel>()) || image[x, y].Equals(Color.Black.ToPixel<TPixel>()))
                    {
                        currentValue &= (byte)~(0x01 << (7 - index));
                    }
                    index = (index + 1) % 8;
                    if (index == 0)
                    {
                        stream.WriteByte(currentValue);
                        currentValue = 0xFF;
                    }
                }
            }
        }
        if (index > 0)
        {
            stream.WriteByte(currentValue);
        }
    }
}