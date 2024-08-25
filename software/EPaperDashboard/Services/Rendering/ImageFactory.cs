using EPaperDashboard.Models.Rendering;
using SixLabors.ImageSharp.PixelFormats;

namespace EPaperDashboard.Services.Rendering;

public class ImageFactory : IImageFactory
{
    public IImage Load(byte[] bytes) => ImageAdapter<Rgba32>.Load(bytes);
}
