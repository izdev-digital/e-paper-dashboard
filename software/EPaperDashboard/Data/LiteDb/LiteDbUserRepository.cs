using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbUserRepository(LiteDbContext context) : IUserRepository
{
    public Maybe<User> FindById(Guid id) =>
        context.Users.FindById(id);

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

    public void Delete(Guid id) =>
        context.Users.Delete(id);
}
