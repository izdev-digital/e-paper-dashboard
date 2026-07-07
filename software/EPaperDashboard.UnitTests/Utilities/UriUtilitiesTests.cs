using EPaperDashboard.Utilities;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Utilities;

public class UriUtilitiesTests
{
    [Fact]
    public void CreateUri_SetsPathAndQueryParameters()
    {
        var baseUri = new Uri("https://example.com");

        var result = UriUtilities.CreateUri(baseUri, "/api/thing", new Dictionary<string, string>
        {
            ["foo"] = "bar",
            ["baz"] = "qux"
        });

        result.AbsolutePath.Should().Be("/api/thing");
        result.Query.Should().Contain("foo=bar").And.Contain("baz=qux");
    }

    [Fact]
    public void CreateUri_NoQueryParameters_ProducesUriWithoutQueryString()
    {
        var baseUri = new Uri("https://example.com");

        var result = UriUtilities.CreateUri(baseUri, "/path", new Dictionary<string, string>());

        result.Query.Should().BeEmpty();
    }

    [Fact]
    public void CreateUri_ValueNeedingEscaping_IsUrlEncoded()
    {
        var baseUri = new Uri("https://example.com");

        var result = UriUtilities.CreateUri(baseUri, "/path", new Dictionary<string, string>
        {
            ["q"] = "a b&c"
        });

        result.Query.Should().Contain("a+b%26c");
    }

    [Fact]
    public void CreateUri_PreservesBaseUriHostAndScheme()
    {
        var baseUri = new Uri("https://homeassistant.local:8123");

        var result = UriUtilities.CreateUri(baseUri, "/api", new Dictionary<string, string>());

        result.Scheme.Should().Be("https");
        result.Host.Should().Be("homeassistant.local");
        result.Port.Should().Be(8123);
    }
}
