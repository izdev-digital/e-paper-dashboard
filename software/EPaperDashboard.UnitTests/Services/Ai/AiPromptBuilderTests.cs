using EPaperDashboard.Models;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class AiPromptBuilderTests
{
    private static AiPromptBuilder CreateSut(params IAiDataSectionFormatter[] formatters) =>
        new(formatters, new FakeTimeProvider(DateTimeOffset.UtcNow));

    private static Dashboard CreateDashboard(string? aiPrompt = null, List<WidgetConfig>? widgets = null) => new()
    {
        Id = DashboardId.New(),
        Name = "Test Dashboard",
        AiPrompt = aiPrompt,
        LayoutConfig = new LayoutConfig { Widgets = widgets ?? [] }
    };

    [Fact]
    public void BuildPrompt_NoHeaderWidgetPresent_UserPromptDoesNotWarnAboutExistingHeader()
    {
        var sut = CreateSut();
        var dashboard = CreateDashboard();

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().NotContain("already exists on this dashboard");
    }

    [Fact]
    public void BuildPrompt_HeaderWidgetAlreadyExists_UserPromptWarnsNotToAddAnother()
    {
        var sut = CreateSut();
        var dashboard = CreateDashboard(widgets: [new WidgetConfig { Type = "header" }]);

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().Contain("already exists on this dashboard");
    }

    [Fact]
    public void BuildPrompt_NoAiPromptSet_UsesDefaultRequestText()
    {
        var sut = CreateSut();
        var dashboard = CreateDashboard(aiPrompt: null);

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().Contain("Create a useful dashboard with the available data.");
    }

    [Fact]
    public void BuildPrompt_AiPromptSet_IncludesItInUserPrompt()
    {
        var sut = CreateSut();
        var dashboard = CreateDashboard(aiPrompt: "Show me the weather and my todos");

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().Contain("Show me the weather and my todos");
    }

    [Fact]
    public void BuildPrompt_FormatterHasNoData_SectionIsOmitted()
    {
        var formatter = new Mock<IAiDataSectionFormatter>();
        formatter.Setup(f => f.HasData(It.IsAny<AiDataSnapshot>())).Returns(false);
        formatter.Setup(f => f.FormatSection(It.IsAny<AiDataSnapshot>())).Returns("SHOULD NOT APPEAR");
        var sut = CreateSut(formatter.Object);
        var dashboard = CreateDashboard();

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().NotContain("SHOULD NOT APPEAR");
        formatter.Verify(f => f.FormatSection(It.IsAny<AiDataSnapshot>()), Times.Never);
    }

    [Fact]
    public void BuildPrompt_FormatterHasData_SectionIsIncluded()
    {
        var formatter = new Mock<IAiDataSectionFormatter>();
        formatter.Setup(f => f.HasData(It.IsAny<AiDataSnapshot>())).Returns(true);
        formatter.Setup(f => f.FormatSection(It.IsAny<AiDataSnapshot>())).Returns("MY SECTION CONTENT");
        var sut = CreateSut(formatter.Object);
        var dashboard = CreateDashboard();

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().Contain("MY SECTION CONTENT");
    }

    [Fact]
    public void BuildPrompt_SystemPromptUsesLayoutConfigGridDimensions()
    {
        var sut = CreateSut();
        var dashboard = CreateDashboard();
        var layoutConfig = new LayoutConfig { GridCols = 10, GridRows = 6, Widgets = [] };

        var (systemPrompt, _) = sut.BuildPrompt(dashboard, layoutConfig, new AiDataSnapshot());

        systemPrompt.Should().Contain("10 columns").And.Contain("6 rows");
    }

    [Fact]
    public void BuildPrompt_UserPromptIncludesCurrentDateTimeFromInjectedTimeProvider()
    {
        var fixedNow = new DateTimeOffset(2026, 3, 17, 9, 30, 0, TimeSpan.Zero);
        var sut = new AiPromptBuilder([], new FakeTimeProvider(fixedNow));
        var dashboard = CreateDashboard();

        var (_, userPrompt) = sut.BuildPrompt(dashboard, dashboard.LayoutConfig!, new AiDataSnapshot());

        userPrompt.Should().Contain("March 17, 2026").And.Contain("9:30 AM");
    }
}
