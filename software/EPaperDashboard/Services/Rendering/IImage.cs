
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace EPaperDashboard.Services.Rendering;

public interface IImage
{
    IImage RotateFlip(RotateMode rotateMode, FlipMode flipMode);

    IImage Quantize(ReadOnlyMemory<Color> palette);

    Task SaveJpegAsync(Stream outStream);

    Task SaveAsync(Stream outStream, IImageEncoder encoder);
}
