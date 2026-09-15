using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;

namespace EPaperDashboard.Services.Rendering;

public class ImageFactory : IImageFactory
{
    public IImage Load(byte[] bytes) => ImageAdapter<Rgba32>.Load(bytes);

    public IImage Load(byte[] bytes, Size expectedSize)
    {
        // Treat bytes returned across the isolation boundary as untrusted before allocating pixels.
        var options = new DecoderOptions { SkipMetadata = true, MaxFrames = 1 };
        var info = SixLabors.ImageSharp.Image.Identify(options, bytes);
        if (info.Width != expectedSize.Width || info.Height != expectedSize.Height)
            throw new InvalidOperationException("The rendering component returned unexpected image dimensions.");
        return ImageAdapter<Rgba32>.Wrap(SixLabors.ImageSharp.Image.Load<Rgba32>(options, bytes));
    }
}
