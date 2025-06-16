using EPaperDashboard.Models;
using LiteDB;
using System.Security.Cryptography;
using System.Text;

namespace EPaperDashboard.Data;

public class UserService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;

    public User? GetUserByUsername(string username) =>
        _dbContext.Users.FindOne(u => u.Username == username);

    public bool HasSuperUser() =>
        _dbContext.Users.Exists(u => u.IsSuperUser);

    public bool IsUserValid(string username, string password) =>
        GetUserByUsername(username) is User user &&
        user.PasswordHash == ComputeSha256Hash(password);

    public bool CreateUser(string username, string password, bool isSuperUser = false)
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
