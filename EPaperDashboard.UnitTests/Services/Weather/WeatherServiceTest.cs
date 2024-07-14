using System.Net;
using EPaperDashboard.Models.Weather;
using EPaperDashboard.Services.Weather;
using FluentResults;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;

namespace EPaperDashboard.UnitTests.Services.Weather;

public class WeatherServiceTest
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Location _locationStub = new("someLocation", 12.34f, 23.45f, "someTimeZone");

    [SetUp]
    public void Setup()
    {
        _httpClientFactoryMock
            .Setup(m => m.CreateClient("WeatherService"))
            .Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
    }

    [Test]
    public async Task GetAsync_FailedToFetchLocation_ReturnsFailedResult()
    {
        LetLocationServiceReturn(Result.Fail("Failed to fetch locaiton information"), targetLocation: "someLocation");
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to fetch locaiton information"));
    }

    [Test]
    public async Task GetAsync_HttpClientThrowsException_ReturnsFailedResult()
    {
        LetLocationServiceReturn(_locationStub, "someLocation");
        _httpMessageHandlerMock
            .Protected()
            .Setup(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Throws<Exception>();
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to fetch weather information"));
    }

    [Test]
    public async Task GetAsync_WithEmptyContent_ReturnsFailedResult()
    {
        LetLocationServiceReturn(_locationStub, "someLocation");
        LetHttpClientReturn(
            content: string.Empty,
            targetUrl: "https://api.open-meteo.com/v1/forecast?latitude=12.34&longitude=23.45&timezone=someTimeZone&daily=weather_code%2capparent_temperature_min%2capparent_temperature_max&forecast_days=1&hourly=apparent_temperature%2cweather_code");
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.One.Message.EqualTo("Failed to convert the weather information json"));
    }

    [Test]
    public async Task GetAsync_WithRequiredInformation_ReturnsSuccessfulResult()
    {
        var json = new JObject(
            new JProperty("hourly", new JObject(
                new JProperty("time", new JArray(
                    new DateTime(2024, 7, 14, 8, 0, 0).ToString("O"),
                    new DateTime(2024, 7, 14, 12, 0, 0).ToString("O"),
                    new DateTime(2024, 7, 14, 16, 0, 0).ToString("O"),
                    new DateTime(2024, 7, 14, 20, 0, 0).ToString("O")
                )),
                new JProperty("apparent_temperature", new JArray(
                    16.1f, 17.1f, 18.1f, 19.1f
                )),
                new JProperty("weather_code", new JArray(
                    1, 2, 3, 4
                ))
            )),
            new JProperty("hourly_units", new JObject(
                new JProperty("apparent_temperature", "degC")
            )),
            new JProperty("daily", new JObject(
                new JProperty("time", new JArray(new DateTime(2024, 7, 14).ToString("O"))),
                new JProperty("apparent_temperature_min", new JArray(12.34)),
                new JProperty("apparent_temperature_max", new JArray(23.45)),
                new JProperty("weather_code", new JArray(123))
            )),
            new JProperty("daily_units", new JObject(
                new JProperty("apparent_temperature_min", "degC"),
                new JProperty("apparent_temperature_max", "degC")
            ))
        );
        LetLocationServiceReturn(_locationStub, "someLocation");
        LetHttpClientReturn(
            content: json.ToString(),
            targetUrl: "https://api.open-meteo.com/v1/forecast?latitude=12.34&longitude=23.45&timezone=someTimeZone&daily=weather_code%2capparent_temperature_min%2capparent_temperature_max&forecast_days=1&hourly=apparent_temperature%2cweather_code");
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        var expectedDailyConditions = new DailyWeatherCondition(
            123,
            new Temperature(12.34f, "degC"),
            new Temperature(23.45f, "degC")
        );
        var expectedHourlyConditions = new[]{
            new WeatherCondition(
                new DateTime(2024, 7, 14, 8, 0, 0),
                1,
                new Temperature(16.1f, "degC")),
            new WeatherCondition(
                new DateTime(2024, 7, 14, 12, 0, 0),
                2,
                new Temperature(17.1f, "degC")),
            new WeatherCondition(
                new DateTime(2024, 7, 14, 16, 0, 0),
                3,
                new Temperature(18.1f, "degC")),
            new WeatherCondition(
                new DateTime(2024, 7, 14, 20, 0, 0),
                4,
                new Temperature(19.1f, "degC"))
        };
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Location, Is.EqualTo("someLocation"));
        Assert.That(result.Value.Daily, Is.EqualTo(expectedDailyConditions));
        Assert.That(result.Value.Hourly, Is.EquivalentTo(expectedHourlyConditions));
    }

    [Test]
    public async Task GetAsync_WithoutWeatherInformation_ReturnsFailedResult()
    {
        LetLocationServiceReturn(_locationStub, "someLocation");
        LetHttpClientReturn(
            content: new JObject().ToString(),
            targetUrl: "https://api.open-meteo.com/v1/forecast?latitude=12.34&longitude=23.45&timezone=someTimeZone&daily=weather_code%2capparent_temperature_min%2capparent_temperature_max&forecast_days=1&hourly=apparent_temperature%2cweather_code");
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Is.EquivalentTo(
            new[]{
                "No daily information is provided",
                "No hourly information is provided",
                "No daily information units is provided",
                "No daily min temperature units is provided",
                "No daily max temperature units is provided",
                "No hourly information units is provided",
                "No hourly temperature units is provided"
            }
        ).Using<IError, string>((a, b) => a.Message == b));
    }

    [Test]
    public async Task GetAsync_WithoutMissingWeatherInformation_ReturnsFailedResult()
    {
        LetLocationServiceReturn(_locationStub, "someLocation");
        LetHttpClientReturn(
            content: new JObject(
            new JProperty("hourly", new JObject()),
            new JProperty("hourly_units", new JObject()),
            new JProperty("daily", new JObject()),
            new JProperty("daily_units", new JObject())).ToString(),
            targetUrl: "https://api.open-meteo.com/v1/forecast?latitude=12.34&longitude=23.45&timezone=someTimeZone&daily=weather_code%2capparent_temperature_min%2capparent_temperature_max&forecast_days=1&hourly=apparent_temperature%2cweather_code");
        var sut = CreateSut();

        var result = await sut.GetAsync("someLocation");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Is.EquivalentTo(
            new[]{
                "No daily min temperature units is provided",
                "No daily max temperature units is provided",
                "No hourly temperature units is provided"
            }
        ).Using<IError, string>((a, b) => a.Message == b));
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

    private void LetLocationServiceReturn(Result<Location> location, string targetLocation)
    {
        _locationServiceMock
           .Setup(m => m.GetLocationDetailsAsync(targetLocation))
           .ReturnsAsync(location);
    }

    private WeatherService CreateSut() => new(_httpClientFactoryMock.Object, _locationServiceMock.Object);
}
