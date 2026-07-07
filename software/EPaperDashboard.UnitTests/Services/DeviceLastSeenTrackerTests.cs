using EPaperDashboard.Models;
using EPaperDashboard.Services;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace EPaperDashboard.UnitTests.Services;

public class DeviceLastSeenTrackerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 17, 8, 0, 0, TimeSpan.Zero);

    private static DeviceLastSeenTracker CreateSut() => new(new FakeTimeProvider(Now));

    [Fact]
    public void ShouldUpdate_FirmwareVersionChanged_ReturnsTrue()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = Now };
        var sut = CreateSut();

        sut.ShouldUpdate(device, "1.1.0").Should().BeTrue();
    }

    [Fact]
    public void ShouldUpdate_NeverSeenBefore_ReturnsTrue()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = null };
        var sut = CreateSut();

        sut.ShouldUpdate(device, "1.0.0").Should().BeTrue();
    }

    [Fact]
    public void ShouldUpdate_LastSeenOverOneMinuteAgo_ReturnsTrue()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = Now.AddMinutes(-1).AddTicks(-1) };
        var sut = CreateSut();

        sut.ShouldUpdate(device, "1.0.0").Should().BeTrue();
    }

    [Fact]
    public void ShouldUpdate_LastSeenExactlyOneMinuteAgo_ReturnsFalse()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = Now.AddMinutes(-1) };
        var sut = CreateSut();

        sut.ShouldUpdate(device, "1.0.0").Should().BeFalse();
    }

    [Fact]
    public void ShouldUpdate_SameFirmwareAndRecentlySeen_ReturnsFalse()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = Now.AddSeconds(-30) };
        var sut = CreateSut();

        sut.ShouldUpdate(device, "1.0.0").Should().BeFalse();
    }

    [Fact]
    public void Apply_SetsFirmwareVersionAndLastSeenAtToCurrentTime()
    {
        var device = new Device { FirmwareVersion = "1.0.0", LastSeenAt = null };
        var sut = CreateSut();

        sut.Apply(device, "2.0.0");

        device.FirmwareVersion.Should().Be("2.0.0");
        device.LastSeenAt.Should().Be(Now);
    }
}
