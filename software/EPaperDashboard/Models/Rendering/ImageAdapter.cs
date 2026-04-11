using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace EPaperDashboard.Models.Rendering;

public sealed class ImageAdapter<TPixel> : IImage
where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Image<TPixel> _image;

    private ImageAdapter(Image<TPixel> image) => _image = image;

    public void Dispose() => _image.Dispose();

    public static ImageAdapter<TPixel> Load(ReadOnlySpan<byte> data)
    {
        var image = Image.Load(data);
        return new(Image.Load<TPixel>(data));
    }

    /// <summary>
    /// Wraps an existing ImageSharp image in an IImage adapter.
    /// </summary>
    public static ImageAdapter<TPixel> Wrap(Image<TPixel> image) => new(image);

    public IImage Quantize(ReadOnlyMemory<Color> palette, IDither? dither)
    {
        _image.Mutate(x => x.Quantize(new PaletteQuantizer(palette, new QuantizerOptions { Dither = dither })));
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

    public IImage Rotate(RotateMode rotateMode)
    {
        _image.Mutate(x => x.Rotate(rotateMode));
        return this;
    }

    public async Task SaveAsync(Stream outStream, IImageEncoder encoder) => await _image.SaveAsync(outStream, encoder);

    public async Task SaveJpegAsync(Stream outStream) => await _image.SaveAsJpegAsync(outStream);
}


