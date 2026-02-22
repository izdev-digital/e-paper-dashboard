using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using System.Security.Cryptography;
using System.Text;

namespace EPaperDashboard.Services;

public sealed class UserService(IUserRepository userRepository, IDashboardRepository dashboardRepository)
{
    public Maybe<User> GetUserByUsername(string username) =>
        userRepository.FindByUsername(username);

    public Maybe<User> GetUserById(UserId id) =>
        userRepository.FindById(id);

    public bool HasSuperUser() =>
        userRepository.ExistsSuperUser();

    public List<User> GetAllUsers() =>
        userRepository.GetAll();

    public bool TryDeleteUser(UserId id) =>
        userRepository.FindById(id)
            .Where(u => !u.IsSuperUser)
            .Match(
                u =>
                {
                    dashboardRepository.DeleteByUserId(u.Id);
                    userRepository.Delete(u.Id);
                    return true;
                },
                () => false
            );

    public bool IsUserValid(string username, string password) =>
        GetUserByUsername(username)
        .Select(user => string.Equals(user.PasswordHash, ComputeSha256Hash(password), StringComparison.OrdinalIgnoreCase))
        .GetValueOrDefault();

    public bool TryCreateUser(string username, string password, bool isSuperUser = false)
    {
        if (userRepository.ExistsByUsername(username))
        {
            return false;
        }

        var user = new User
        {
            Username = username,
            PasswordHash = ComputeSha256Hash(password),
            IsSuperUser = isSuperUser
        };

        userRepository.Insert(user);
        return true;
    }

    public bool TryChangeNickname(UserId userId, string? newNickname) =>
        GetUserById(userId)
        .Match(
            user =>
            {
                user.Nickname = string.IsNullOrWhiteSpace(newNickname) ? null : newNickname;
                userRepository.Update(user);
                return true;
            },
            () => false);

    public bool TryChangePassword(UserId userId, string oldPassword, string newPassword) =>
        userRepository.FindById(userId)
            .Where(user => string.Equals(user.PasswordHash, ComputeSha256Hash(oldPassword), StringComparison.Ordinal))
            .Match(
                user =>
                {
                    var newHash = ComputeSha256Hash(newPassword);
                    if (string.Equals(user.PasswordHash, newHash, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    user.PasswordHash = newHash;
                    userRepository.Update(user);
                    return true;
                },
                () => false
            );

    public static string ComputeSha256Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
