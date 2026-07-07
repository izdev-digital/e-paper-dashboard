using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services;

public class PairingServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Mock<IPairingSessionRepository> CreateRepo() => new();

    private static PairingService CreateSut(Mock<IPairingSessionRepository> repo, TimeProvider? timeProvider = null) =>
        new(repo.Object, timeProvider ?? new FakeTimeProvider(FixedNow));

    [Fact]
    public void CreatePairingSession_SetsCreatedAtToCurrentTime()
    {
        var repo = CreateRepo();
        var sut = CreateSut(repo);

        var session = sut.CreatePairingSession(UserId.New());

        session.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void CreatePairingSession_SetsExpiryFiveMinutesAfterCreation()
    {
        var repo = CreateRepo();
        var sut = CreateSut(repo);

        var session = sut.CreatePairingSession(UserId.New());

        session.ExpiresAt.Should().Be(FixedNow.AddMinutes(5));
    }

    [Fact]
    public void CreatePairingSession_GeneratesSixCharacterUppercaseAlphanumericCode()
    {
        var repo = CreateRepo();
        var sut = CreateSut(repo);

        var session = sut.CreatePairingSession(UserId.New());

        session.Code.Should().MatchRegex("^[0-9A-Z]{6}$");
    }

    [Fact]
    public void CreatePairingSession_PersistsSessionViaRepository()
    {
        var repo = CreateRepo();
        var sut = CreateSut(repo);

        var session = sut.CreatePairingSession(UserId.New());

        repo.Verify(r => r.Insert(session), Times.Once);
        session.Status.Should().Be(PairingStatus.Pending);
        session.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void RegisterDevice_UnknownCode_ReturnsMaybeNone()
    {
        var repo = CreateRepo();
        repo.Setup(r => r.FindByCode("BADCOD")).Returns(Maybe<PairingSession>.None);
        var sut = CreateSut(repo);

        var result = sut.RegisterDevice("BADCOD", "device-1");

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void RegisterDevice_ValidCode_CompletesSessionAndAssignsApiKey()
    {
        var repo = CreateRepo();
        var session = new PairingSession { Code = "ABC123", Status = PairingStatus.Pending };
        repo.Setup(r => r.FindByCode("ABC123")).Returns(session);
        var sut = CreateSut(repo);

        var result = sut.RegisterDevice("ABC123", "device-1");

        result.HasValue.Should().BeTrue();
        result.Value.Status.Should().Be(PairingStatus.Completed);
        result.Value.IsCompleted.Should().BeTrue();
        result.Value.DeviceIdentifier.Should().Be("device-1");
        result.Value.ApiKey.Should().NotBeNullOrEmpty();
        repo.Verify(r => r.Update(session), Times.Once);
    }

    [Fact]
    public void RegisterDevice_WithScreenDimensions_NormalizesWidthAsMaxAndHeightAsMin()
    {
        var repo = CreateRepo();
        var session = new PairingSession { Code = "ABC123" };
        repo.Setup(r => r.FindByCode("ABC123")).Returns(session);
        var sut = CreateSut(repo);

        // portrait device reports width < height
        var result = sut.RegisterDevice("ABC123", "device-1", screenWidth: 480, screenHeight: 800);

        result.Value.ScreenWidth.Should().Be(800);
        result.Value.ScreenHeight.Should().Be(480);
    }

    [Fact]
    public void RegisterDevice_WithoutScreenDimensions_LeavesScreenSizeUnset()
    {
        var repo = CreateRepo();
        var session = new PairingSession { Code = "ABC123" };
        repo.Setup(r => r.FindByCode("ABC123")).Returns(session);
        var sut = CreateSut(repo);

        var result = sut.RegisterDevice("ABC123", "device-1");

        result.Value.ScreenWidth.Should().BeNull();
        result.Value.ScreenHeight.Should().BeNull();
    }

    [Fact]
    public void HasActiveSessions_DelegatesToRepositoryWithCurrentTime()
    {
        var repo = CreateRepo();
        repo.Setup(r => r.HasActiveSessions(FixedNow)).Returns(true);
        var sut = CreateSut(repo);

        sut.HasActiveSessions().Should().BeTrue();
        repo.Verify(r => r.HasActiveSessions(FixedNow), Times.Once);
    }

    [Fact]
    public void CleanupExpiredSessions_DelegatesToRepositoryWithCurrentTime()
    {
        var repo = CreateRepo();
        var sut = CreateSut(repo);

        sut.CleanupExpiredSessions();

        repo.Verify(r => r.DeleteExpired(FixedNow), Times.Once);
    }
}
