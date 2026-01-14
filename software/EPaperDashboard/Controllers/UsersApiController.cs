using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using LiteDB;
using System.Security.Claims;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersApiController(UserService userService) : ControllerBase
{
    private ObjectId UserId => new(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

    [HttpPost("change-nickname")]
    public IActionResult ChangeNickname([FromBody] ChangeNicknameRequest request)
    {
        if (userService.TryChangeNickname(UserId, request.NewNickname))
        {
            return Ok(new { message = string.IsNullOrWhiteSpace(request.NewNickname) ? "Nickname cleared." : "Nickname changed successfully." });
        }

        return BadRequest(new { message = "Nickname change failed." });
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || 
            string.IsNullOrWhiteSpace(request.NewPassword) || 
            string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
        {
            return BadRequest(new { message = "All password fields are required." });
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "New password and confirmation do not match." });
        }

        if (userService.TryChangePassword(UserId, request.CurrentPassword, request.NewPassword))
        {
            return Ok(new { message = "Password changed successfully." });
        }

        return BadRequest(new { message = "Password change failed." });
    }

    [HttpDelete("delete-profile")]
    public IActionResult DeleteProfile()
    {
        if (userService.TryDeleteUser(UserId))
        {
            return Ok(new { message = "Profile deleted successfully." });
        }

        return BadRequest(new { message = "Failed to delete profile." });
    }
}

public record ChangeNicknameRequest(string NewNickname);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
