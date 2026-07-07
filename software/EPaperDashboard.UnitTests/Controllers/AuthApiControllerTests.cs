using System.Security.Claims;
using CSharpFunctionalExtensions;
using EPaperDashboard.Controllers;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using EPaperDashboard.Services;
using EPaperDashboard.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EPaperDashboard.UnitTests.Controllers;

public class AuthApiControllerTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IDashboardRepository> _dashboardRepository = new();
    private readonly Mock<IDeploymentStrategy> _deploymentStrategy = new();
    private readonly Mock<IAuthenticationService> _authenticationService = new();

    private AuthApiController CreateSut(UserId? userId = null, bool isHomeAssistantIngress = false)
    {
        var controller = new AuthApiController(
            new UserService(_userRepository.Object, _dashboardRepository.Object),
            _deploymentStrategy.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_authenticationService.Object);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.Value));
        }
        if (isHomeAssistantIngress)
        {
            claims.Add(new Claim(Constants.HomeAssistantIngressClaim, "true"));
        }
        if (claims.Count > 0)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var sut = CreateSut();

        var result = await sut.Login(new LoginRequest("alice", "wrong"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_ValidCredentials_SignsInAndReturnsUserInfo()
    {
        var user = new User { Username = "alice", PasswordHash = UserService.ComputeSha256Hash("secret") };
        _userRepository.Setup(r => r.FindByUsername("alice")).Returns(user);
        var sut = CreateSut();

        var result = await sut.Login(new LoginRequest("alice", "secret"));

        result.Should().BeOfType<OkObjectResult>();
        _authenticationService.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task Register_UsernameAlreadyExists_ReturnsBadRequest()
    {
        _userRepository.Setup(r => r.FindByUsername("bob")).Returns(new User { Username = "bob" });
        var sut = CreateSut();

        var result = await sut.Register(new RegisterRequest("bob", "pw"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_NewUsername_CreatesUserSignsInAndReturnsUserInfo()
    {
        _userRepository.Setup(r => r.FindByUsername("newuser")).Returns(Maybe<User>.None);
        User? inserted = null;
        _userRepository.Setup(r => r.Insert(It.IsAny<User>())).Callback<User>(u =>
        {
            inserted = u;
            // Simulate the repository now being able to find the user it just persisted.
            _userRepository.Setup(r => r.FindByUsername("newuser")).Returns(u);
        });
        var sut = CreateSut();

        var result = await sut.Register(new RegisterRequest("newuser", "pw"));

        result.Should().BeOfType<OkObjectResult>();
        inserted.Should().NotBeNull();
        inserted!.IsSuperUser.Should().BeFalse();
        _authenticationService.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public async Task Logout_SignsOutAndReturnsOk()
    {
        var sut = CreateSut();

        var result = await sut.Logout();

        result.Should().BeOfType<OkObjectResult>();
        _authenticationService.Verify(a => a.SignOutAsync(
            It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
    }

    [Fact]
    public void GetCurrentUser_NotAuthenticated_ReturnsUnauthorized()
    {
        var sut = CreateSut();

        var result = sut.GetCurrentUser();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void GetCurrentUser_HomeAssistantIngress_ReturnsSimplifiedInfoWithoutUserLookup()
    {
        _deploymentStrategy.SetupGet(d => d.Mode).Returns(DeploymentMode.Addon);
        var sut = CreateSut(Constants.HomeAssistantVirtualUserId, isHomeAssistantIngress: true);

        var result = sut.GetCurrentUser();

        result.Should().BeOfType<OkObjectResult>();
        _userRepository.Verify(r => r.FindById(It.IsAny<UserId>()), Times.Never);
    }

    [Fact]
    public void GetCurrentUser_StandaloneModeUserNotFound_ReturnsUnauthorized()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(Maybe<User>.None);
        var sut = CreateSut(userId);

        var result = sut.GetCurrentUser();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void GetCurrentUser_StandaloneModeUserFound_ReturnsUserDetails()
    {
        var userId = UserId.New();
        _userRepository.Setup(r => r.FindById(userId)).Returns(new User { Id = userId, Username = "alice" });
        _deploymentStrategy.SetupGet(d => d.Mode).Returns(DeploymentMode.Standalone);
        var sut = CreateSut(userId);

        var result = sut.GetCurrentUser();

        result.Should().BeOfType<OkObjectResult>();
    }
}
