using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IUserRepository
{
    Maybe<User> FindById(Guid id);
    Maybe<User> FindByUsername(string username);
    List<User> GetAll();
    bool ExistsByUsername(string username);
    bool ExistsSuperUser();
    void Insert(User user);
    void Update(User user);
    void Delete(Guid id);
}
