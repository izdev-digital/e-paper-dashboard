using EPaperDashboard.Guards;
using EPaperDashboard.Models;
using EPaperDashboard.UnitTests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EPaperDashboard.UnitTests.Guards;

public class DashboardOwnerFromBodyAttributeTests
{
    private static readonly DashboardOwnerFromBodyAttribute Sut = new();

    [Fact]
    public async Task OnAuthorizationAsync_MalformedJsonBody_SetsBadRequest()
    {
        var context = AuthorizationFilterContextBuilder.Build(userIdClaim: "x", requestBody: "{ not json");

        await Sut.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_BodyMissingDashboardId_SetsBadRequest()
    {
        var context = AuthorizationFilterContextBuilder.Build(userIdClaim: "x", requestBody: "{}");

        await Sut.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OnAuthorizationAsync_ValidDashboardIdInBody_OwnerMatches_LeavesResultNull()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId };
        var context = AuthorizationFilterContextBuilder.Build(
            userIdClaim: userId.Value,
            requestBody: $$"""{"dashboardId": "{{dashboardId.Value}}"}""",
            configure: (users, dashboards) =>
            {
                users.Setup(r => r.FindById(userId)).Returns(new User { Id = userId });
                dashboards.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
            });

        await Sut.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsync_PascalCaseDashboardIdKey_IsAlsoAccepted()
    {
        var userId = UserId.New();
        var dashboardId = DashboardId.New();
        var dashboard = new Dashboard { Id = dashboardId, UserId = userId };
        var context = AuthorizationFilterContextBuilder.Build(
            userIdClaim: userId.Value,
            requestBody: $$"""{"DashboardId": "{{dashboardId.Value}}"}""",
            configure: (users, dashboards) =>
            {
                users.Setup(r => r.FindById(userId)).Returns(new User { Id = userId });
                dashboards.Setup(r => r.FindById(dashboardId)).Returns(dashboard);
            });

        await Sut.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }
}
