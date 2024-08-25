using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace EPaperDashboard.Models.Rendering;

public interface IImage
{
    IImage RotateFlip(RotateMode rotateMode, FlipMode flipMode);

    IImage Quantize(ReadOnlyMemory<Color> palette);

    IImage Resize(Size size);

    Task SaveJpegAsync(Stream outStream);

    Task SaveAsync(Stream outStream, IImageEncoder encoder);
}
