using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services.Rendering;
using Moq;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class PageToImageRenderingServiceTest
{
    private readonly Mock<IWebDriver> _webDriverMock = new();
    private readonly Mock<IImageFactory> _imageFactoryMock = new();

    [Test]
    public async Task RenderPageAsync_WebDriverThrowsException_ReturnsFailure()
    {
        var someUri = new Uri("https://somedashboard.com");
        var someSize = new Size(123, 234);
        _webDriverMock
            .Setup(m => m.GetScreenshotAsync(someUri, someSize))
            .ThrowsAsync(new Exception("Failed to take a screenshot"));
        var sut = CreateSut();

        var result = await sut.RenderPageAsync(someUri, someSize);

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Has.One.With.Message.EqualTo("Failed to render page to image"));
        Assert.That(result.Errors.First().Reasons, Has.One.With.Message.EqualTo("Failed to take a screenshot"));
    }

    [Test]
    public async Task RenderPageAsync_SuccessfullyLoadedImage_ReturnsImage()
    {
        var someUri = new Uri("https://somedashboard.com");
        var imageSize = new Size(123, 234);
        var imageBuffer = new byte[] { 1, 2, 3 };
        var imageMock = new Mock<IImage>();
        imageMock
            .Setup(m => m.Resize(imageSize))
            .Returns(() => imageMock.Object);
        _imageFactoryMock
            .Setup(m => m.Load(imageBuffer))
            .Returns(() => imageMock.Object);
        _webDriverMock
            .Setup(m => m.GetScreenshotAsync(someUri, imageSize))
            .ReturnsAsync(imageBuffer);
        var sut = CreateSut();

        var result = await sut.RenderPageAsync(someUri, imageSize);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.SameAs(imageMock.Object));
    }

    private PageToImageRenderingService CreateSut() =>
        new(_webDriverMock.Object, _imageFactoryMock.Object);
}