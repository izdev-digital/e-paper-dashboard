using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class BinaryEncoderTests
{
    private static byte[] Encode(Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        new BlackRedWhiteBinaryEncoder().Encode(image, stream);
        return stream.ToArray();
    }

    [Fact]
    public void Encode_AllWhitePixels_ProducesAllOnesForBothPlanes()
    {
        using var image = new Image<Rgba32>(8, 1);
        image.Mutate(ctx => ctx.Fill(Color.White));

        Encode(image).Should().Equal(0xFF, 0xFF);
    }

    [Fact]
    public void Encode_AllBlackPixels_ProducesAllZeroBlackPlaneAndAllOneRedPlane()
    {
        using var image = new Image<Rgba32>(8, 1);
        image.Mutate(ctx => ctx.Fill(Color.Black));

        Encode(image).Should().Equal(0x00, 0xFF);
    }

    [Fact]
    public void Encode_AllRedPixels_ProducesAllOneBlackPlaneAndAllZeroRedPlane()
    {
        using var image = new Image<Rgba32>(8, 1);
        image.Mutate(ctx => ctx.Fill(Color.Red));

        Encode(image).Should().Equal(0xFF, 0x00);
    }

    [Fact]
    public void Encode_MixedColors_ProducesExpectedBitPattern()
    {
        // 8x1 image: pixel 0 black, pixel 1 red, pixels 2-7 white.
        using var image = new Image<Rgba32>(8, 1);
        image.Mutate(ctx => ctx.Fill(Color.White));
        image[0, 0] = Color.Black.ToPixel<Rgba32>();
        image[1, 0] = Color.Red.ToPixel<Rgba32>();

        // Bit 7 (MSB) corresponds to pixel 0, bit 6 to pixel 1, etc.
        // Black plane: bit 7 cleared (0x7F). Red plane: bit 6 cleared (0xBF).
        Encode(image).Should().Equal(0x7F, 0xBF);
    }

    [Fact]
    public void Encode_PixelCountNotMultipleOfEight_ThrowsArgumentException()
    {
        using var image = new Image<Rgba32>(3, 3); // 9 pixels
        using var stream = new MemoryStream();

        var act = () => new BlackRedWhiteBinaryEncoder().Encode(image, stream);

        act.Should().Throw<ArgumentException>().WithMessage("*multiple of 8*");
    }

    [Fact]
    public void Encode_LargerImage_ProducesTwoBytesPerEightPixels()
    {
        using var image = new Image<Rgba32>(16, 1);
        image.Mutate(ctx => ctx.Fill(Color.White));

        var result = Encode(image);

        result.Should().HaveCount(4); // 16 pixels / 8 per byte-pair * 2 bytes (black+red)
    }
}
