using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace EPaperDashboard.Models.Rendering;

public sealed class ImageAdapter<TPixel> : IImage
where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Image<TPixel> _image;

    private ImageAdapter(Image<TPixel> image) => _image = image;

    public static ImageAdapter<TPixel> Load(ReadOnlySpan<byte> data) => new(Image.Load<TPixel>(data));

    public IImage Quantize(ReadOnlyMemory<Color> palette)
    {
        _image.Mutate(x => x.Quantize(new PaletteQuantizer(
            palette,
            new QuantizerOptions
            {
                Dither = KnownDitherings.Atkinson,
                MaxColors = 3
            })));
        return this;
    }

    public IImage Resize(Size size)
    {
        _image.Mutate(x => x.Resize(size.Width, size.Height));
        return this;
    }

    public IImage RotateFlip(RotateMode rotateMode, FlipMode flipMode)
    {
        _image.Mutate(x => x.RotateFlip(rotateMode, flipMode));
        return this;
    }

    public async Task SaveAsync(Stream outStream, IImageEncoder encoder) => await _image.SaveAsync(outStream, encoder);

    public async Task SaveJpegAsync(Stream outStream) => await _image.SaveAsJpegAsync(outStream);
}


