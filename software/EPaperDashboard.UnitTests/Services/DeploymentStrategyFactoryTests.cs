using EPaperDashboard.Services;
using EPaperDashboard.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services;

public class DeploymentStrategyFactoryTests
{
    [Fact]
    public void Create_HostMode_ReturnsHostModeStrategy()
    {
        var result = DeploymentStrategyFactory.Create(
            DeploymentMode.Host, Mock.Of<IEnvironmentConfiguration>(), NullLoggerFactory.Instance);

        result.Should().BeOfType<HostModeStrategy>();
        result.Mode.Should().Be(DeploymentMode.Host);
    }

    [Fact]
    public void Create_StandaloneMode_ReturnsStandaloneStrategy()
    {
        var result = DeploymentStrategyFactory.Create(
            DeploymentMode.Standalone, Mock.Of<IEnvironmentConfiguration>(), NullLoggerFactory.Instance);

        result.Should().BeOfType<StandaloneStrategy>();
        result.Mode.Should().Be(DeploymentMode.Standalone);
    }
}
