using EPaperDashboard.Models.Rendering;

namespace EPaperDashboard.Services.Rendering;

public interface IImageFactory
{
    IImage Load(byte[] bytes);
}
