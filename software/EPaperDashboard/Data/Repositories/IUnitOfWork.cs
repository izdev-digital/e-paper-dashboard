namespace EPaperDashboard.Data.Repositories;

public interface IUnitOfWork
{
    T Execute<T>(Func<T> operation);
}
