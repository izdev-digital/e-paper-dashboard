using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace EPaperDashboard.Controllers;

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