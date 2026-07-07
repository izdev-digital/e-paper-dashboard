using EPaperDashboard.Guards;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EPaperDashboard.UnitTests.Guards;

public class DeviceAccessGuardTests
{
    private static Endpoint EndpointWith(params object[] metadata) =>
        new(null, new EndpointMetadataCollection(metadata), "test");

    [Fact]
    public void IsAccessible_NullEndpoint_ReturnsFalse()
    {
        DeviceAccessGuard.IsAccessible(null).Should().BeFalse();
    }

    [Fact]
    public void IsAccessible_NoDeviceAccessibleAttribute_ReturnsFalse()
    {
        var endpoint = EndpointWith();

        DeviceAccessGuard.IsAccessible(endpoint).Should().BeFalse();
    }

    [Fact]
    public void IsAccessible_DeviceAccessibleAttributePresent_ReturnsTrue()
    {
        var endpoint = EndpointWith(new DeviceAccessibleAttribute());

        DeviceAccessGuard.IsAccessible(endpoint).Should().BeTrue();
    }

    [Fact]
    public void RequiresActivePairing_NullEndpoint_ReturnsFalse()
    {
        DeviceAccessGuard.RequiresActivePairing(null).Should().BeFalse();
    }

    [Fact]
    public void RequiresActivePairing_AttributeWithoutFlag_ReturnsFalse()
    {
        var endpoint = EndpointWith(new DeviceAccessibleAttribute());

        DeviceAccessGuard.RequiresActivePairing(endpoint).Should().BeFalse();
    }

    [Fact]
    public void RequiresActivePairing_AttributeWithFlagSet_ReturnsTrue()
    {
        var endpoint = EndpointWith(new DeviceAccessibleAttribute { RequireActivePairing = true });

        DeviceAccessGuard.RequiresActivePairing(endpoint).Should().BeTrue();
    }
}
