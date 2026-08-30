using System.Text.Json;
using CSharpFunctionalExtensions;
using EPaperDashboard.Models.Rendering;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Providers;

public class SsrDataProviderTests
{
    private readonly Mock<IEntityStateProvider> _entityStateProvider = new();
    private readonly Mock<ITodoDataProvider> _todoDataProvider = new();
    private readonly Mock<ICalendarDataProvider> _calendarDataProvider = new();
    private readonly Mock<IWeatherForecastProvider> _weatherForecastProvider = new();
    private readonly Mock<IRssFeedDataProvider> _rssFeedDataProvider = new();
    private readonly Mock<IEntityHistoryProvider> _entityHistoryProvider = new();
    private readonly Mock<IAiContentProvider> _aiContentProvider = new();

    private SsrDataProvider CreateSut()
    {
        _entityStateProvider
            .Setup(p => p.FetchEntityStatesAsync(It.IsAny<string>(), It.IsAny<string[]>()))
            .ReturnsAsync(Result.Success<List<HassEntityState>, string>(new List<HassEntityState>()));

        return new SsrDataProvider(
            _entityStateProvider.Object,
            _todoDataProvider.Object,
            _calendarDataProvider.Object,
            _weatherForecastProvider.Object,
            _rssFeedDataProvider.Object,
            _entityHistoryProvider.Object,
            _aiContentProvider.Object,
            NullLogger<SsrDataProvider>.Instance);
    }

    private static WidgetConfigEntry Widget(string type, object config, string id = "w1") => new(
        id, type, new WidgetPositionConfig(0, 0, 1, 1), JsonSerializer.SerializeToElement(config), null);

    private static LayoutConfig LayoutWith(params WidgetConfigEntry[] widgets) =>
        new(100, 100, 4, 4, null!, [.. widgets], 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public async Task FetchSsrDataAsync_NoWidgets_ReturnsEmptyData()
    {
        var sut = CreateSut();

        var result = await sut.FetchSsrDataAsync("dash1", LayoutWith());

        result.TodoItems.Should().BeEmpty();
        result.CalendarEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchSsrDataAsync_TodoWidgetWithEntityId_PopulatesTodoItems()
    {
        _todoDataProvider
            .Setup(p => p.FetchTodoItemsAsync("dash1", "todo.list"))
            .ReturnsAsync(Result.Success<List<TodoItem>, string>([new TodoItem { Summary = "Buy milk" }]));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("todo", new { entityId = "todo.list" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.TodoItems.Should().ContainKey("todo.list");
        result.TodoItems["todo.list"].Should().ContainSingle(t => t.Summary == "Buy milk");
    }

    [Fact]
    public async Task FetchSsrDataAsync_CalendarWidget_PopulatesCalendarEvents()
    {
        _calendarDataProvider
            .Setup(p => p.FetchCalendarEventsAsync("dash1", "calendar.a", 168))
            .ReturnsAsync(Result.Success<List<CalendarEvent>, string>([new CalendarEvent { Summary = "Meeting" }]));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("calendar", new { entityId = "calendar.a" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.CalendarEvents.Should().ContainKey("calendar.a");
    }

    [Fact]
    public async Task FetchSsrDataAsync_WeatherForecastWidget_PopulatesForecastListFromResultDictionary()
    {
        var forecastList = new List<object?> { "entry1" };
        _weatherForecastProvider
            .Setup(p => p.FetchWeatherForecastAsync("dash1", "weather.home", "daily"))
            .ReturnsAsync(Result.Success<Dictionary<string, object?>, string>(
                new Dictionary<string, object?> { ["forecast"] = forecastList }));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("weather-forecast", new { entityId = "weather.home" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        var key = WeatherForecastDataKey.Create("weather.home", "daily");
        result.WeatherForecasts.Should().ContainKey(key);
        result.WeatherForecasts[key].Should().BeSameAs(forecastList);
    }

    [Fact]
    public async Task FetchSsrDataAsync_WeatherForecastWidgetHourlyMode_RequestsHourlyForecastType()
    {
        _weatherForecastProvider
            .Setup(p => p.FetchWeatherForecastAsync("dash1", "weather.home", "hourly"))
            .ReturnsAsync(Result.Success<Dictionary<string, object?>, string>(
                new Dictionary<string, object?> { ["forecast"] = new List<object?>() }));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("weather-forecast", new { entityId = "weather.home", forecastMode = "hourly" }));

        await sut.FetchSsrDataAsync("dash1", layout);

        _weatherForecastProvider.Verify(p => p.FetchWeatherForecastAsync("dash1", "weather.home", "hourly"), Times.Once);
    }

    [Fact]
    public async Task FetchSsrDataAsync_SameWeatherEntityWithDifferentModes_KeepsBothForecasts()
    {
        var daily = new List<object?> { "daily" };
        var hourly = new List<object?> { "hourly" };
        _weatherForecastProvider
            .Setup(p => p.FetchWeatherForecastAsync("dash1", "weather.home", "daily"))
            .ReturnsAsync(Result.Success<Dictionary<string, object?>, string>(
                new Dictionary<string, object?> { ["forecast"] = daily }));
        _weatherForecastProvider
            .Setup(p => p.FetchWeatherForecastAsync("dash1", "weather.home", "hourly"))
            .ReturnsAsync(Result.Success<Dictionary<string, object?>, string>(
                new Dictionary<string, object?> { ["forecast"] = hourly }));
        var sut = CreateSut();
        var layout = LayoutWith(
            Widget("weather-forecast", new { entityId = "weather.home", forecastMode = "daily" }, "daily"),
            Widget("weather-forecast", new { entityId = "weather.home", forecastMode = "hourly" }, "hourly"));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.WeatherForecasts[WeatherForecastDataKey.Create("weather.home", "daily")]
            .Should().BeSameAs(daily);
        result.WeatherForecasts[WeatherForecastDataKey.Create("weather.home", "hourly")]
            .Should().BeSameAs(hourly);
    }

    [Fact]
    public async Task FetchSsrDataAsync_DuplicateTodoEntity_FetchesOnce()
    {
        _todoDataProvider
            .Setup(p => p.FetchTodoItemsAsync("dash1", "todo.list"))
            .ReturnsAsync(Result.Success<List<TodoItem>, string>([]));
        var sut = CreateSut();
        var layout = LayoutWith(
            Widget("todo", new { entityId = "todo.list" }, "todo-1"),
            Widget("todo", new { entityId = "todo.list" }, "todo-2"));

        await sut.FetchSsrDataAsync("dash1", layout);

        _todoDataProvider.Verify(p => p.FetchTodoItemsAsync("dash1", "todo.list"), Times.Once);
    }

    [Fact]
    public async Task FetchSsrDataAsync_RssFeedWidget_PopulatesRssEntries()
    {
        _rssFeedDataProvider
            .Setup(p => p.FetchRssFeedEntriesAsync("dash1", "sensor.feed"))
            .ReturnsAsync(Result.Success<List<RssFeedEntry>, string>([new RssFeedEntry { Title = "Headline" }]));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("rss-feed", new { entityId = "sensor.feed" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.RssFeedEntries.Should().ContainKey("sensor.feed");
    }

    [Theory]
    [InlineData("1h", 1)]
    [InlineData("6h", 6)]
    [InlineData("24h", 24)]
    [InlineData("7d", 168)]
    [InlineData("30d", 720)]
    [InlineData("unknown", 24)]
    public async Task FetchSsrDataAsync_GraphWidget_TranslatesPeriodToHours(string period, int expectedHours)
    {
        _entityHistoryProvider
            .Setup(p => p.FetchEntityHistoryAsync("dash1", It.IsAny<IEnumerable<string>>(), expectedHours))
            .ReturnsAsync(Result.Success<Dictionary<string, List<HistoryState>>, string>(
                new Dictionary<string, List<HistoryState>> { ["sensor.temp"] = [] }));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("graph", new
        {
            series = new[] { new { entityId = "sensor.temp" } },
            period
        }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.HistoryData.Should().ContainKey("sensor.temp");
    }

    [Fact]
    public async Task FetchSsrDataAsync_SameGraphEntityWithDifferentPeriods_FetchesLongestPeriodOnce()
    {
        _entityHistoryProvider
            .Setup(p => p.FetchEntityHistoryAsync("dash1", It.IsAny<IEnumerable<string>>(), 720))
            .ReturnsAsync(Result.Success<Dictionary<string, List<HistoryState>>, string>(
                new Dictionary<string, List<HistoryState>> { ["sensor.temp"] = [] }));
        var sut = CreateSut();
        var layout = LayoutWith(
            Widget("graph", new { series = new[] { new { entityId = "sensor.temp" } }, period = "1h" }, "graph-1"),
            Widget("graph", new { series = new[] { new { entityId = "sensor.temp" } }, period = "30d" }, "graph-2"));

        await sut.FetchSsrDataAsync("dash1", layout);

        _entityHistoryProvider.Verify(
            p => p.FetchEntityHistoryAsync(
                "dash1",
                It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "sensor.temp" })),
                720),
            Times.Once);
        _entityHistoryProvider.Verify(
            p => p.FetchEntityHistoryAsync("dash1", It.IsAny<IEnumerable<string>>(), It.Is<int>(hours => hours != 720)),
            Times.Never);
    }

    [Fact]
    public async Task FetchSsrDataAsync_AiContentWidgetWithCachedContent_UsesCacheWithoutGenerating()
    {
        _aiContentProvider.Setup(p => p.GetCachedContent("dash1", "w1")).Returns("cached text");
        var sut = CreateSut();
        var layout = LayoutWith(Widget("ai-content", new { prompt = "summarize" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.AiContent.Should().ContainKey("w1").WhoseValue.Should().Be("cached text");
        _aiContentProvider.Verify(
            p => p.GenerateAndCacheContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FetchSsrDataAsync_AiContentWidgetNoCache_GeneratesAndStoresContent()
    {
        _aiContentProvider.Setup(p => p.GetCachedContent("dash1", "w1")).Returns((string?)null);
        _aiContentProvider
            .Setup(p => p.GenerateAndCacheContentAsync("dash1", "w1", "summarize", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<string, string>("freshly generated"));
        var sut = CreateSut();
        var layout = LayoutWith(Widget("ai-content", new { prompt = "summarize" }));

        var result = await sut.FetchSsrDataAsync("dash1", layout);

        result.AiContent["w1"].Should().Be("freshly generated");
    }

    [Fact]
    public async Task FetchSsrDataAsync_HeaderWidgetWithBadgeEntityIds_IncludesThemInEntityStatesFetch()
    {
        var sut = CreateSut();
        var layout = LayoutWith(Widget("header", new
        {
            title = "Home",
            badges = new[] { new { entityId = "sensor.temp" } }
        }));

        await sut.FetchSsrDataAsync("dash1", layout);

        _entityStateProvider.Verify(
            p => p.FetchEntityStatesAsync("dash1", It.Is<string[]>(ids => ids.Contains("sensor.temp"))),
            Times.Once);
    }
}
