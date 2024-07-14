using System.Collections;
using System.Net;
using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather;
using FluentResults;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;

namespace EPaperDashboard.UnitTests.Services.Weather;

public class LocationServiceTest
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

    [SetUp]
    public void Setup()
    {
        _httpClientFactoryMock
            .Setup(m => m.CreateClient("WeatherService"))
            .Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
    }

    [Test]
    public async Task GetLocationDetailsAsync_HttpClientFails_ReturnsFailedResult()
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Throws<Exception>();
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to fetch location information"));
    }

    [Theory]
    public async Task GetLocationDetailsAsync_WithoutContentWithDifferentStatusCodes_ReturnsFailedResult(HttpStatusCode statusCode)
    {
        LetHttpClientReturn(
            new HttpResponseMessage { StatusCode = statusCode },
            targetUrl: "https://geocoding-api.open-meteo.com/v1/search?name=someLocation&count=1&format=json");
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to deserialize location object"));
    }

    [Test]
    public async Task GetLocationDetailsAsync_WithoutContent_ReturnsFailedResult()
    {
        LetHttpClientReturn(
            new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(string.Empty) },
            targetUrl: "https://geocoding-api.open-meteo.com/v1/search?name=someLocation&count=1&format=json");
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to deserialize location object"));
    }

    [TestCaseSource(nameof(FailureTestCases))]
    public async Task GetLocationDetailsAsync_WithoutLocationName_ReturnsFailedResult(JObject json, string expectedError)
    {
        LetHttpClientReturn(
            json.ToString(),
            targetUrl: "https://geocoding-api.open-meteo.com/v1/search?name=someLocation&count=1&format=json");
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Has.One.Message.EqualTo(expectedError));
    }

    public static IEnumerable FailureTestCases() => new (JObject Json, string ExpectedError)[]
    {
        (
        Json: new JObject(new JProperty("results", new JArray())),
        ExpectedError: "Information about location was not found"),
        (
        Json: new JObject(new JProperty("results", new JArray(
            new JObject(
                new JProperty("latitude", "12.34"),
                new JProperty("longitude", "23.45"),
                new JProperty("timezone", "someTimeZone"))))),
        ExpectedError: "Location name is not available"),
        (
        Json: new JObject(new JProperty("results", new JArray(
            new JObject(
                new JProperty("name", "someLocationName"),
                new JProperty("longitude", "23.45"),
                new JProperty("timezone", "someTimeZone"))))),
        ExpectedError: "Latitude is not available"),
        (
        Json: new JObject(new JProperty("results", new JArray(
            new JObject(
                new JProperty("name", "someLocationName"),
                new JProperty("latitude", "12.34"),
                new JProperty("timezone", "someTimeZone"))))),
        ExpectedError: "Longitude is not available"),
        (
        Json: new JObject(new JProperty("results", new JArray(
            new JObject(
                new JProperty("name", "someLocationName"),
                new JProperty("latitude", "12.34"),
                new JProperty("longitude", "23.45"))))),
        ExpectedError: "Time zone is not available")
    }.Select(x => new TestCaseData(x.Json, x.ExpectedError));

    [Test]
    public async Task GetLocationDetailsAsync_WithoutLocationName_ReturnsFailedResult()
    {
        LetHttpClientReturn(
            new JObject(new JProperty("results", new JArray(new JObject()))).ToString(),
            targetUrl: "https://geocoding-api.open-meteo.com/v1/search?name=someLocation&count=1&format=json");
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        Assert.That(result.IsFailed, Is.True);
        Assert.That(result.Errors, Is.EquivalentTo(new[]{
            "Location name is not available",
            "Time zone is not available",
            "Latitude is not available",
            "Longitude is not available"
        }).Using<IError, string>((a, b) => a.Message == b));
    }

    [Test]
    public async Task GetLocationDetailsAsync_WithRequiredInformation_ReturnsSuccessfulResult()
    {
        LetHttpClientReturn(
            new JObject(new JProperty("results", new JArray(
                new JObject(
                    new JProperty("name", "someLocationName"),
                    new JProperty("latitude", "12.34"),
                    new JProperty("longitude", "23.45"),
                    new JProperty("timezone", "someTimeZone"))))).ToString(),
            targetUrl: "https://geocoding-api.open-meteo.com/v1/search?name=someLocation&count=1&format=json");
        var sut = CreateSut();

        var result = await sut.GetLocationDetailsAsync("someLocation");

        var expectedResult = new Location("someLocationName", 12.34f, 23.45f, "someTimeZone");
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(expectedResult));
    }

    private void LetHttpClientReturn(HttpResponseMessage response, string targetUrl)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsoluteUri == targetUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void LetHttpClientReturn(string content, string targetUrl)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsoluteUri == targetUrl),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content)
            });
    }

    private LocationService CreateSut() =>
        new(_httpClientFactoryMock.Object);
}
