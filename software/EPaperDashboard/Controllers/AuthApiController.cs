using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EPaperDashboard.Services;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthApiController(UserService userService) : ControllerBase
{
    private readonly UserService _userService = userService;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!_userService.IsUserValid(request.Username, request.Password))
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var user = _userService.GetUserByUsername(request.Username);
        if (user.HasNoValue)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Value.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Value.Username),
            new Claim("IsSuperUser", user.Value.IsSuperUser.ToString().ToLower())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new
        {
            id = user.Value.Id.ToString(),
            username = user.Value.Username,
            nickname = user.Value.Nickname,
            isSuperUser = user.Value.IsSuperUser
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = _userService.GetUserByUsername(request.Username);
        if (existingUser.HasValue)
        {
            return BadRequest(new { message = "Username already exists." });
        }

        if (!_userService.TryCreateUser(request.Username, request.Password, isSuperUser: false))
        {
            return BadRequest(new { message = "Failed to create user." });
        }

        var user = _userService.GetUserByUsername(request.Username);
        if (user.HasNoValue)
        {
            return BadRequest(new { message = "Failed to retrieve created user." });
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Value.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Value.Username),
            new Claim("IsSuperUser", user.Value.IsSuperUser.ToString().ToLower())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Ok(new
        {
            id = user.Value.Id.ToString(),
            username = user.Value.Username,
            nickname = user.Value.Nickname,
            isSuperUser = user.Value.IsSuperUser
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("current")]
    public IActionResult GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized(new { message = "Not authenticated." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var isSuperUser = User.FindFirst("IsSuperUser")?.Value == "true";
        var isHAIngress = User.FindFirst("HomeAssistantIngress")?.Value == "true";

        // In Home Assistant mode, return simplified user info
        if (isHAIngress)
        {
            return Ok(new
            {
                id = userId,
                username = username,
                nickname = username,
                isSuperUser = isSuperUser,
                isHomeAssistantMode = true
            });
        }

        // In standalone mode, get full user details from database
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = _userService.GetUserById(new LiteDB.ObjectId(userId));
        if (user.HasNoValue)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            id = user.Value.Id.ToString(),
            username = user.Value.Username,
            nickname = user.Value.Nickname,
            isSuperUser = user.Value.IsSuperUser,
            isHomeAssistantMode = false
        });
    }
}

public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Password);
