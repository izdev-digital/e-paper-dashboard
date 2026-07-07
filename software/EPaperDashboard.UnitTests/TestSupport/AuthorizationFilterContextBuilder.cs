using System.Security.Claims;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Services;
using EPaperDashboard.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EPaperDashboard.UnitTests.TestSupport;

/// <summary>
/// Builds a minimal, real <see cref="AuthorizationFilterContext"/> for testing authorization
/// filter attributes without spinning up the full ASP.NET Core pipeline.
/// </summary>
internal static class AuthorizationFilterContextBuilder
{
    public static AuthorizationFilterContext Build(
        Mock<IUserRepository>? userRepository = null,
        Mock<IDashboardRepository>? dashboardRepository = null,
        string? userIdClaim = null,
        bool isHomeAssistantIngress = false,
        string? routeDashboardId = null,
        string? requestBody = null,
        Action<Mock<IUserRepository>, Mock<IDashboardRepository>>? configure = null)
    {
        userRepository ??= new Mock<IUserRepository>();
        dashboardRepository ??= new Mock<IDashboardRepository>();
        configure?.Invoke(userRepository, dashboardRepository);

        var services = new ServiceCollection();
        services.AddSingleton(new UserService(userRepository.Object, dashboardRepository.Object));
        services.AddSingleton(new DashboardService(dashboardRepository.Object));
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var claims = new List<Claim>();
        if (userIdClaim is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdClaim));
        }
        if (isHomeAssistantIngress)
        {
            claims.Add(new Claim(Constants.HomeAssistantIngressClaim, "true"));
        }
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        if (requestBody is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(requestBody);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
        }

        var routeData = new RouteData();
        if (routeDashboardId is not null)
        {
            routeData.Values["dashboardId"] = routeDashboardId;
        }

        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            httpContext,
            routeData,
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }
}
