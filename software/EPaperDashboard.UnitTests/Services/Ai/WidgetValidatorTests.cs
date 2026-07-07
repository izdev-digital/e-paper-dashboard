using System.Text.Json;
using EPaperDashboard.Models;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class WidgetValidatorTests
{
    private static WidgetValidator CreateSut() => new(NullLogger<WidgetValidator>.Instance);

    private static Dashboard CreateDashboard(string name = "My Dashboard") => new()
    {
        Id = DashboardId.New(),
        Name = name
    };

    private static WidgetConfig Widget(string type, object config) => new()
    {
        Id = "w1",
        Type = type,
        Config = JsonSerializer.SerializeToElement(config)
    };

    [Fact]
    public void ValidateAndRepair_MarkdownWidgetWithContent_IsKept()
    {
        var sut = CreateSut();
        var widget = Widget("markdown", new { content = "hello" });

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), CreateDashboard());

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
    }

    [Fact]
    public void ValidateAndRepair_MarkdownWidgetWithEmptyContent_IsDropped()
    {
        var sut = CreateSut();
        var widget = Widget("markdown", new { content = "" });

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), CreateDashboard());

        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndRepair_CalendarWidgetWithUnknownEntityId_IsDropped()
    {
        var sut = CreateSut();
        var widget = Widget("calendar", new { entityId = "calendar.unknown" });
        var aiData = new AiDataSnapshot();

        var result = sut.ValidateAndRepair([widget], aiData, CreateDashboard());

        result.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndRepair_CalendarWidgetWithKnownEntityId_IsKept()
    {
        var sut = CreateSut();
        var widget = Widget("calendar", new { entityId = "calendar.known" });
        var aiData = new AiDataSnapshot
        {
            CalendarEvents = { ["calendar.known"] = [] }
        };

        var result = sut.ValidateAndRepair([widget], aiData, CreateDashboard());

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
    }

    [Fact]
    public void ValidateAndRepair_HeaderWidgetMissingTitle_RepairsTitleWithDashboardName()
    {
        var sut = CreateSut();
        var widget = Widget("header", new { });
        var dashboard = CreateDashboard("Kitchen Display");

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), dashboard);

        result.Should().ContainSingle();
        var repaired = result[0];
        repaired.Config.GetProperty("title").GetString().Should().Be("Kitchen Display");
    }

    [Fact]
    public void ValidateAndRepair_HeaderWidgetWithTitle_IsKeptUnchanged()
    {
        var sut = CreateSut();
        var widget = Widget("header", new { title = "Custom Title" });

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), CreateDashboard());

        result.Should().ContainSingle();
        result[0].Config.GetProperty("title").GetString().Should().Be("Custom Title");
    }

    [Fact]
    public void ValidateAndRepair_UnrecognizedWidgetType_IsKeptUnchanged()
    {
        var sut = CreateSut();
        var widget = Widget("some-future-widget", new { anything = "goes" });

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), CreateDashboard());

        result.Should().ContainSingle().Which.Should().BeSameAs(widget);
    }

    [Fact]
    public void ValidateAndRepair_GraphWidgetWithNoMatchingSeriesEntities_IsDropped()
    {
        var sut = CreateSut();
        var widget = Widget("graph", new { series = new[] { new { entityId = "sensor.unknown" } } });

        var result = sut.ValidateAndRepair([widget], new AiDataSnapshot(), CreateDashboard());

        result.Should().BeEmpty();
    }
}
