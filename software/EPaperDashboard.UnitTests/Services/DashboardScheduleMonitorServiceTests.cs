using EPaperDashboard.Services;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services;

public class DashboardScheduleMonitorServiceTests
{
    [Fact]
    public void CalculateExpectedUpdateTime_NoScheduledTimes_ReturnsNull()
    {
        var now = new DateTimeOffset(2026, 3, 17, 9, 0, 0, TimeSpan.Zero);

        var result = DashboardScheduleMonitorService.CalculateExpectedUpdateTime(
            [], now, TimeOnly.FromDateTime(now.DateTime));

        result.Should().BeNull();
    }

    [Fact]
    public void CalculateExpectedUpdateTime_ScheduleAlreadyPassedToday_ReturnsTodayAtThatTime()
    {
        var now = new DateTimeOffset(2026, 3, 17, 9, 0, 0, TimeSpan.Zero);
        var updateTimes = new List<TimeOnly> { new(8, 0) };

        var result = DashboardScheduleMonitorService.CalculateExpectedUpdateTime(
            updateTimes, now, TimeOnly.FromDateTime(now.DateTime));

        result.Should().Be(new DateTimeOffset(2026, 3, 17, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CalculateExpectedUpdateTime_ScheduleNotYetReachedToday_ReturnsYesterdaysOccurrence()
    {
        var now = new DateTimeOffset(2026, 3, 17, 6, 0, 0, TimeSpan.Zero);
        var updateTimes = new List<TimeOnly> { new(8, 0) };

        var result = DashboardScheduleMonitorService.CalculateExpectedUpdateTime(
            updateTimes, now, TimeOnly.FromDateTime(now.DateTime));

        result.Should().Be(new DateTimeOffset(2026, 3, 16, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CalculateExpectedUpdateTime_MultipleSchedulesToday_PicksMostRecentPassedOne()
    {
        var now = new DateTimeOffset(2026, 3, 17, 13, 0, 0, TimeSpan.Zero);
        var updateTimes = new List<TimeOnly> { new(8, 0), new(12, 0), new(18, 0) };

        var result = DashboardScheduleMonitorService.CalculateExpectedUpdateTime(
            updateTimes, now, TimeOnly.FromDateTime(now.DateTime));

        result.Should().Be(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void CalculateExpectedUpdateTime_MultipleSchedulesNoneReachedYet_PicksLatestFromYesterday()
    {
        var now = new DateTimeOffset(2026, 3, 17, 7, 0, 0, TimeSpan.Zero);
        var updateTimes = new List<TimeOnly> { new(8, 0), new(12, 0), new(18, 0) };

        var result = DashboardScheduleMonitorService.CalculateExpectedUpdateTime(
            updateTimes, now, TimeOnly.FromDateTime(now.DateTime));

        result.Should().Be(new DateTimeOffset(2026, 3, 16, 18, 0, 0, TimeSpan.Zero));
    }
}
