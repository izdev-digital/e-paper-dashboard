using EPaperDashboard.Models;
using LiteDB;
using System.Security.Cryptography;
using System.Text;

namespace EPaperDashboard.Data;

public class UserService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public User? GetUserByUsername(string username) => _dbContext.Users.FindOne(u => u.Username == username);

    public bool HasSuperUser() => _dbContext.Users.Exists(u => u.IsSuperUser);

    public List<User> GetAllUsers() => [.. _dbContext.Users.FindAll()];

    public bool TryDeleteUser(ObjectId id)
    {
        return _dbContext.Users.FindById(id) is User user
            && !user.IsSuperUser
            && _dbContext.Users.Delete(id);
    }

    public bool IsUserValid(string username, string password) =>
        GetUserByUsername(username) is User user &&
        user.PasswordHash == ComputeSha256Hash(password);

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

    public bool ChangeUsername(string currentUsername, string newUsername)
    {
        if (string.IsNullOrWhiteSpace(newUsername) || _dbContext.Users.Exists(u => u.Username == newUsername))
            return false;
        var user = _dbContext.Users.FindOne(u => u.Username == currentUsername);
        if (user == null)
            return false;
        user.Username = newUsername;
        _dbContext.Users.Update(user);
        return true;
    }

    public bool ChangeNickname(string username, string? newNickname)
    {
        var user = _dbContext.Users.FindOne(u => u.Username == username);
        if (user == null)
            return false;
        user.Nickname = string.IsNullOrWhiteSpace(newNickname) ? null : newNickname;
        _dbContext.Users.Update(user);
        return true;
    }

    public bool DeleteUserByUsername(string username)
    {
        var user = _dbContext.Users.FindOne(u => u.Username == username);
        if (user == null || user.IsSuperUser)
            return false;
        return _dbContext.Users.Delete(user.Id);
    }

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
