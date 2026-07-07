using System.Text.Json;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class AiPreGenerationServiceTests
{
    // AiPreGenerationService reads DateTimeOffset.LocalDateTime, which converts based on
    // the *running machine's* local time zone. Building instants from an Unspecified-kind
    // DateTime makes the offset match the local zone automatically, so LocalDateTime round-trips
    // to the exact wall-clock values below regardless of where the test runs.
    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified));

    private static readonly DateTimeOffset Now = Local(2026, 3, 17, 8, 0);

    private static Dashboard CreateDashboard(
        bool isAiEnabled = true,
        string? aiPrompt = "hi",
        List<TimeOnly>? updateTimes = null,
        int leadTimeMinutes = 5,
        DateTimeOffset? lastAiGenerationTime = null) => new()
    {
        Id = DashboardId.New(),
        IsAiEnabled = isAiEnabled,
        AiPrompt = aiPrompt,
        RenderingMode = RenderingMode.Custom,
        UpdateTimes = updateTimes ?? [new TimeOnly(8, 0)],
        AiLeadTimeMinutes = leadTimeMinutes,
        LastAiGenerationTime = lastAiGenerationTime
    };

    [Theory]
    [InlineData(7, 56, true)]  // 4 minutes before update, within 5-min lead window
    [InlineData(8, 0, true)]   // exactly at update time
    [InlineData(7, 54, false)] // 6 minutes before, outside window
    [InlineData(8, 1, false)]  // 1 minute after update, window has passed
    public void ShouldPreGenerateDashboard_ChecksLeadTimeWindowAroundScheduledUpdate(int hour, int minute, bool expected)
    {
        var dashboard = CreateDashboard(updateTimes: [new TimeOnly(8, 0)], leadTimeMinutes: 5);
        var now = Local(2026, 3, 17, hour, minute);

        AiPreGenerationService.ShouldPreGenerateDashboard(dashboard, now).Should().Be(expected);
    }

    [Fact]
    public void ShouldPreGenerateDashboard_AiDisabled_ReturnsFalse()
    {
        var dashboard = CreateDashboard(isAiEnabled: false);

        AiPreGenerationService.ShouldPreGenerateDashboard(dashboard, Now).Should().BeFalse();
    }

    [Fact]
    public void ShouldPreGenerateDashboard_NoAiPrompt_ReturnsFalse()
    {
        var dashboard = CreateDashboard(aiPrompt: "   ");

        AiPreGenerationService.ShouldPreGenerateDashboard(dashboard, Now).Should().BeFalse();
    }

    [Fact]
    public void ShouldPreGenerateDashboard_AlreadyGeneratedWithinWindow_ReturnsFalse()
    {
        var dashboard = CreateDashboard(
            updateTimes: [new TimeOnly(8, 0)],
            leadTimeMinutes: 5,
            lastAiGenerationTime: Local(2026, 3, 17, 7, 56));
        var now = Local(2026, 3, 17, 7, 58);

        AiPreGenerationService.ShouldPreGenerateDashboard(dashboard, now).Should().BeFalse();
    }

    // Regression test for a real bug: the "already generated" check in IsInPreGenerationWindow used
    // to compare only time-of-day (TimeOnly.FromDateTime drops the date), so a generation from a
    // PREVIOUS day at the same time-of-day still counted as "already generated" for every future
    // occurrence of that window — silently stopping daily AI dashboards from ever regenerating after
    // their first successful run. Fixed by anchoring the dedup window to today's date.
    [Fact]
    public void ShouldPreGenerateDashboard_PreviousGenerationWasOnADifferentDayAtSameTimeOfDay_IsNotTreatedAsAlreadyGenerated()
    {
        var dashboard = CreateDashboard(
            updateTimes: [new TimeOnly(8, 0)],
            leadTimeMinutes: 5,
            lastAiGenerationTime: Local(2026, 3, 16, 7, 56)); // yesterday
        var now = Local(2026, 3, 17, 7, 57);

        AiPreGenerationService.ShouldPreGenerateDashboard(dashboard, now).Should().BeTrue();
    }

    [Fact]
    public void IsInPreGenerationWindow_NoUpdateTimesConfigured_ReturnsFalse()
    {
        var dashboard = CreateDashboard(updateTimes: []);

        AiPreGenerationService.IsInPreGenerationWindow(dashboard, Now, null).Should().BeFalse();
    }

    [Fact]
    public void IsInPreGenerationWindow_HandlesMidnightCrossing()
    {
        var dashboard = CreateDashboard(updateTimes: [new TimeOnly(0, 2)], leadTimeMinutes: 5);
        var now = Local(2026, 3, 17, 23, 59);

        AiPreGenerationService.IsInPreGenerationWindow(dashboard, now, null).Should().BeTrue();
    }

    [Fact]
    public void ShouldPreGenerateContentWidgets_NoAiContentWidgets_ReturnsFalse()
    {
        var dashboard = CreateDashboard();
        dashboard.LayoutConfig = new LayoutConfig { Widgets = [new WidgetConfig { Type = "markdown" }] };

        AiPreGenerationService.ShouldPreGenerateContentWidgets(dashboard, Now).Should().BeFalse();
    }

    [Fact]
    public void ShouldPreGenerateContentWidgets_HasAiContentWidgetWithPrompt_ChecksWindow()
    {
        var dashboard = CreateDashboard(updateTimes: [new TimeOnly(8, 0)], leadTimeMinutes: 5);
        dashboard.LayoutConfig = new LayoutConfig
        {
            Widgets =
            [
                new WidgetConfig
                {
                    Type = "ai-content",
                    Config = JsonSerializer.SerializeToElement(new { prompt = "summarize my day" })
                }
            ]
        };
        var now = Local(2026, 3, 17, 7, 58);

        AiPreGenerationService.ShouldPreGenerateContentWidgets(dashboard, now).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 0, 5, 5)]
    [InlineData(23, 59, 0, 1, 2)]
    public void GetMinutesDifference_ComputesExpectedMinutes(int fromH, int fromM, int toH, int toM, double expected)
    {
        var from = new TimeOnly(fromH, fromM);
        var to = new TimeOnly(toH, toM);

        AiPreGenerationService.GetMinutesDifference(from, to).Should().Be(expected);
    }

    [Fact]
    public void HasEffectiveAiConfig_DashboardHasHomeAssistantAiConfig_ReturnsTrue()
    {
        var dashboard = new Dashboard { AiConfig = new AiConfig { ConnectionMode = AiConnectionMode.HomeAssistant } };
        var userService = new UserService(Mock.Of<IUserRepository>(), Mock.Of<IDashboardRepository>());

        AiPreGenerationService.HasEffectiveAiConfig(dashboard, userService).Should().BeTrue();
    }

    [Fact]
    public void HasEffectiveAiConfig_UserHasNoAiConfig_ReturnsFalse()
    {
        var dashboard = new Dashboard { UserId = UserId.New() };
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.FindById(dashboard.UserId)).Returns(new User { Id = dashboard.UserId, AiConfig = null });
        var userService = new UserService(users.Object, Mock.Of<IDashboardRepository>());

        AiPreGenerationService.HasEffectiveAiConfig(dashboard, userService).Should().BeFalse();
    }
}
