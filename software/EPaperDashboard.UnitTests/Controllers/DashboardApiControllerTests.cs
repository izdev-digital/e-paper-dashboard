using CSharpFunctionalExtensions;
using EPaperDashboard.Controllers;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using EPaperDashboard.UnitTests.TestSupport;
using EPaperDashboard.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Controllers;

public class DashboardApiControllerTests
{
    private readonly Mock<IDashboardRepository> _dashboardRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IDeploymentStrategy> _deploymentStrategy = new();

    private DashboardApiController CreateSut() => new(
        new DashboardService(_dashboardRepository.Object),
        new UserService(_userRepository.Object, _dashboardRepository.Object),
        _deploymentStrategy.Object);

    [Fact]
    public void GetDashboards_UserDoesNotExist_ReturnsUnauthorized()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(Maybe<User>.None);
        var sut = CreateSut().WithUser(userId);

        var result = sut.GetDashboards();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void GetDashboards_UserExists_ReturnsDashboardsForThatUser()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(new User { Id = userId });
        _dashboardRepository.Setup(r => r.FindByUserId(userId)).Returns([new Dashboard { UserId = userId }]);
        var sut = CreateSut().WithUser(userId);

        var result = sut.GetDashboards();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<List<Dashboard>>().Which.Should().ContainSingle();
    }

    [Fact]
    public void GetDashboards_HomeAssistantIngress_SkipsUserLookupAndUsesVirtualUserId()
    {
        _dashboardRepository.Setup(r => r.FindByUserId(Constants.HomeAssistantVirtualUserId)).Returns([]);
        var sut = CreateSut().WithHomeAssistantIngressUser();

        var result = sut.GetDashboards();

        result.Should().BeOfType<OkObjectResult>();
        _userRepository.Verify(r => r.FindById(It.IsAny<UserId>()), Times.Never);
    }

    [Fact]
    public void GetDashboard_InvalidId_ReturnsBadRequest()
    {
        var sut = CreateSut().WithUser(UserId.New());

        var result = sut.GetDashboard("not-a-valid-id");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void GetDashboard_NotFound_ReturnsNotFound()
    {
        var dashboardId = DashboardId.New();
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(Maybe<Dashboard>.None);
        var sut = CreateSut().WithUser(UserId.New());

        var result = sut.GetDashboard(dashboardId.Value);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetDashboard_OwnedByDifferentUser_ReturnsForbid()
    {
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = UserId.New() };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(UserId.New());

        var result = sut.GetDashboard(dashboardId.Value);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void GetDashboard_OwnedByCurrentUser_ReturnsDashboardDto()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId, Name = "My Dashboard" };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(userId);

        var result = sut.GetDashboard(dashboardId.Value);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<DashboardResponseDto>().Which.Name.Should().Be("My Dashboard");
    }

    [Fact]
    public void CreateDashboard_UnauthenticatedUser_ReturnsUnauthorized()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(Maybe<User>.None);
        var sut = CreateSut().WithUser(userId);

        var result = sut.CreateDashboard(new CreateDashboardRequest("New Dashboard", null, null, null, null));

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void CreateDashboard_InvalidScreenSize_ReturnsBadRequest()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(new User { Id = userId });
        var sut = CreateSut().WithUser(userId);

        var result = sut.CreateDashboard(new CreateDashboardRequest("New Dashboard", null, null, ScreenWidth: 123, ScreenHeight: 456));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void CreateDashboard_ValidRequest_PersistsAndReturnsDto()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(new User { Id = userId });
        var sut = CreateSut().WithUser(userId);

        var result = sut.CreateDashboard(new CreateDashboardRequest("New Dashboard", "desc", null, null, null));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<DashboardResponseDto>().Which.Name.Should().Be("New Dashboard");
        _dashboardRepository.Verify(r => r.Insert(It.Is<Dashboard>(d => d.UserId == userId && d.Name == "New Dashboard")), Times.Once);
    }

    [Fact]
    public void UpdateDashboard_ClearAccessTokenTrue_SetsAccessTokenNull()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId, AccessToken = "old-token" };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(userId);

        sut.UpdateDashboard(dashboardId.Value, new UpdateDashboardRequest(
            null, null, null, ClearAccessToken: true, null, null, null, null, null, null, null, null, null, null, null, null));

        _dashboardRepository.Verify(r => r.Update(It.Is<Dashboard>(d => d.AccessToken == null)), Times.Once);
    }

    [Fact]
    public void UpdateDashboard_OwnedByDifferentUser_ReturnsForbid()
    {
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = UserId.New() };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(UserId.New());

        var result = sut.UpdateDashboard(dashboardId.Value, new UpdateDashboardRequest(
            "x", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null));

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void UpdateDashboard_AiEnabledButNoEffectiveAiConfig_AutoDisablesAi()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId, IsAiEnabled = false };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        _userRepository.Setup(r => r.FindById(userId)).Returns(new User { Id = userId, AiConfig = null });
        var sut = CreateSut().WithUser(userId);

        sut.UpdateDashboard(dashboardId.Value, new UpdateDashboardRequest(
            null, null, null, null, null, null, null, null, null, null, null, null, null, IsAiEnabled: true, null, null));

        _dashboardRepository.Verify(r => r.Update(It.Is<Dashboard>(d => d.IsAiEnabled == false)), Times.Once);
    }

    [Fact]
    public void DeleteDashboard_OwnedByCurrentUser_DeletesAndReturnsOk()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(userId);

        var result = sut.DeleteDashboard(dashboardId.Value);

        result.Should().BeOfType<OkObjectResult>();
        _dashboardRepository.Verify(r => r.Delete(dashboardId), Times.Once);
    }

    [Fact]
    public void DeleteDashboard_NotOwner_ReturnsForbidAndDoesNotDelete()
    {
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = UserId.New() };
        _dashboardRepository.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
        var sut = CreateSut().WithUser(UserId.New());

        var result = sut.DeleteDashboard(dashboardId.Value);

        result.Should().BeOfType<ForbidResult>();
        _dashboardRepository.Verify(r => r.Delete(It.IsAny<DashboardId>()), Times.Never);
    }
}
