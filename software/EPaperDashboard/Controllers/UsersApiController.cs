using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EPaperDashboard.Services;
using LiteDB;
using System.Security.Claims;

namespace EPaperDashboard.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersApiController(UserService userService) : BaseApiController
{
    [HttpPost("change-nickname")]
    public IActionResult ChangeNickname([FromBody] ChangeNicknameRequest request)
    {
        if (userService.TryChangeNickname(CurrentUserId, request.NewNickname))
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

        if (userService.TryChangePassword(CurrentUserId, request.CurrentPassword, request.NewPassword))
        {
            return Ok(new { message = "Password changed successfully." });
        }

        return BadRequest(new { message = "Password change failed." });
    }

    [HttpDelete("delete-profile")]
    public IActionResult DeleteProfile()
    {
        if (userService.TryDeleteUser(CurrentUserId))
        {
            return Ok(new { message = "Profile deleted successfully." });
        }

        return BadRequest(new { message = "Failed to delete profile." });
    }

    [HttpGet("all")]
    [Authorize(Policy = "SuperUserOnly")]
    public IActionResult GetAllUsers()
    {
        var users = userService.GetAllUsers();
        return Ok(users.Select(u => new { u.Id, u.Username, u.Nickname, u.IsSuperUser }));
    }

    [HttpPost("add")]
    [Authorize(Policy = "SuperUserOnly")]
    public IActionResult AddUser([FromBody] AddUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        if (userService.TryCreateUser(request.Username, request.Password))
        {
            return Ok(new { message = "User added successfully." });
        }

        return BadRequest(new { message = "User already exists." });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperUserOnly")]
    public IActionResult DeleteUser(string id)
    {
        try
        {
            var objectId = new ObjectId(id);
            if (userService.TryDeleteUser(objectId))
            {
                return Ok(new { message = "User deleted successfully." });
            }

            return BadRequest(new { message = "Cannot delete user." });
        }
        catch
        {
            return BadRequest(new { message = "Invalid user id." });
        }
    }
}

public record ChangeNicknameRequest(string NewNickname);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
public record AddUserRequest(string Username, string Password);
