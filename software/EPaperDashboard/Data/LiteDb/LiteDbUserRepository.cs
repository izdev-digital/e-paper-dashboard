using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using LiteDB;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbUserRepository(LiteDbContext context) : IUserRepository
{
    public Maybe<User> FindById(UserId id) =>
        context.Users.FindById(new ObjectId(id.Value));

    public Maybe<User> FindByUsername(string username) =>
        context.Users.FindOne(u => u.Username == username);

    public List<User> GetAll() =>
        [.. context.Users.FindAll()];

    public bool ExistsByUsername(string username) =>
        context.Users.Exists(u => u.Username == username);

    public bool ExistsSuperUser() =>
        context.Users.Exists(u => u.IsSuperUser);

    public void Insert(User user) =>
        context.Users.Insert(user);

    public void Update(User user) =>
        context.Users.Update(user);

    public void Delete(UserId id) =>
        context.Users.Delete(new ObjectId(id.Value));
}
