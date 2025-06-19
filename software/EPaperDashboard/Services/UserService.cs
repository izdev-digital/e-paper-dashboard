using CSharpFunctionalExtensions;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;
using System.Security.Cryptography;
using System.Text;

namespace EPaperDashboard.Services;

public sealed class UserService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public Maybe<User> GetUserByUsername(string username) =>
        _dbContext.Users.FindOne(u => u.Username == username);

    public Maybe<User> GetUserById(ObjectId id) =>
        _dbContext.Users.FindById(id);

    public bool HasSuperUser() =>
        _dbContext.Users.Exists(u => u.IsSuperUser);

    public List<User> GetAllUsers() =>
        [.. _dbContext.Users.FindAll()];

    private void DeleteDashboardsForUser(User user) =>
        _dbContext.Dashboards.DeleteMany(d => d.UserId == user.Id);

    public bool TryDeleteUser(ObjectId id) =>
        _dbContext.Users
            .FindById(id).AsMaybe()
            .Where(u => !u.IsSuperUser)
            .Match(
                u =>
                {
                    DeleteDashboardsForUser(u);
                    _dbContext.Users.Delete(u.Id);
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
        if (_dbContext.Users.Exists(u => u.Username == username))
        {
            return false;
        }

        var user = new User
        {
            Username = username,
            PasswordHash = ComputeSha256Hash(password),
            IsSuperUser = isSuperUser
        };

        _dbContext.Users.Insert(user);
        return true;
    }

    public bool TryChangeNickname(string username, string? newNickname) =>
        GetUserByUsername(username)
        .Select(user =>
        {
            user.Nickname = string.IsNullOrWhiteSpace(newNickname) ? null : newNickname;
            return user;
        })
        .Match(
            user =>
            {
                _dbContext.Users.Update(user);
                return true;
            },
            () => false);

    public bool TryDeleteUserByUsername(string username) =>
        GetUserByUsername(username)
        .Match(user => TryDeleteUser(user.Id), () => false);

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
