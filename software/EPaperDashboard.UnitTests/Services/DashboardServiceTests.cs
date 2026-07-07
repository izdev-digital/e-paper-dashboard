using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Services;

public class DashboardServiceTests
{
    [Fact]
    public void GetDashboardById_RepositoryReturnsNone_PropagatesMaybeNone()
    {
        var repo = new Mock<IDashboardRepository>();
        repo.Setup(r => r.FindById(It.IsAny<DashboardId>())).Returns(Maybe<Dashboard>.None);
        var sut = new DashboardService(repo.Object);

        var result = sut.GetDashboardById(DashboardId.New());

        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetDashboardById_RepositoryReturnsDashboard_ReturnsSameDashboard()
    {
        var dashboard = new Dashboard { Id = DashboardId.New() };
        var repo = new Mock<IDashboardRepository>();
        repo.Setup(r => r.FindById(dashboard.Id)).Returns(dashboard);
        var sut = new DashboardService(repo.Object);

        var result = sut.GetDashboardById(dashboard.Id);

        result.HasValue.Should().BeTrue();
        result.Value.Should().BeSameAs(dashboard);
    }

    [Fact]
    public void GetDashboardsForUser_CallsRepositoryWithGivenUserId()
    {
        var userId = UserId.New();
        var repo = new Mock<IDashboardRepository>();
        repo.Setup(r => r.FindByUserId(userId)).Returns([]);
        var sut = new DashboardService(repo.Object);

        sut.GetDashboardsForUser(userId);

        repo.Verify(r => r.FindByUserId(userId), Times.Once);
    }

    [Fact]
    public void AddDashboard_InsertsIntoRepository()
    {
        var dashboard = new Dashboard { Id = DashboardId.New() };
        var repo = new Mock<IDashboardRepository>();
        var sut = new DashboardService(repo.Object);

        sut.AddDashboard(dashboard);

        repo.Verify(r => r.Insert(dashboard), Times.Once);
    }

    [Fact]
    public void DeleteDashboard_DeletesFromRepositoryById()
    {
        var id = DashboardId.New();
        var repo = new Mock<IDashboardRepository>();
        var sut = new DashboardService(repo.Object);

        sut.DeleteDashboard(id);

        repo.Verify(r => r.Delete(id), Times.Once);
    }
}
