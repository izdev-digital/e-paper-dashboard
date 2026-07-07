using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class FontAwesomeIconRegistryTests
{
    // No real wwwroot / fa-icons.json exists in the test environment, so the registry falls back
    // to its built-in icon set (LoadFallbackIcons) — this is the same fallback production hits
    // when the frontend build hasn't generated fa-icons.json yet.
    private static FontAwesomeIconRegistry CreateSut() =>
        new(Mock.Of<IWebHostEnvironment>(), NullLogger<FontAwesomeIconRegistry>.Instance);

    [Fact]
    public void TryGetIcon_KnownFallbackIcon_ReturnsTrue()
    {
        var sut = CreateSut();

        sut.TryGetIcon("temperature-half", out var entry).Should().BeTrue();
        entry.Path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryGetIcon_WithFaPrefix_StripsPrefixBeforeLookup()
    {
        var sut = CreateSut();

        sut.TryGetIcon("fa-temperature-half", out var entry).Should().BeTrue();
        entry.Path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryGetIcon_CaseInsensitive_StillMatches()
    {
        var sut = CreateSut();

        sut.TryGetIcon("TEMPERATURE-HALF", out _).Should().BeTrue();
    }

    [Fact]
    public void TryGetIcon_UnknownIcon_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.TryGetIcon("not-a-real-icon", out _).Should().BeFalse();
    }

    [Fact]
    public void Count_FallbackIconSet_IsNonZero()
    {
        var sut = CreateSut();

        sut.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetParsedPath_ValidFallbackIcon_ReturnsNonNullPathWithNonZeroBounds()
    {
        var sut = CreateSut();
        sut.TryGetIcon("sun", out var entry);

        var path = sut.GetParsedPath("sun", entry);

        path.Should().NotBeNull();
        path!.Bounds.Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetParsedPath_CalledTwiceForSameKey_ReturnsCachedResult()
    {
        var sut = CreateSut();
        sut.TryGetIcon("sun", out var entry);

        var first = sut.GetParsedPath("sun", entry);
        var second = sut.GetParsedPath("sun", entry);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetParsedPath_UnparseablePathData_ReturnsNullInsteadOfThrowing()
    {
        var sut = CreateSut();
        var badEntry = new FontAwesomeIconRegistry.IconEntry(Path: "", VbW: 0, VbH: 0);

        var act = () => sut.GetParsedPath("bad-icon-key", badEntry);

        act.Should().NotThrow();
    }
}
