namespace EPaperDashboard.Data.Repositories;

public interface IUnitOfWork
{
    T Execute<T>(Func<T> operation);
}

internal sealed class ImmediateUnitOfWork : IUnitOfWork
{
    public static ImmediateUnitOfWork Instance { get; } = new();

    private ImmediateUnitOfWork() { }

    public T Execute<T>(Func<T> operation) => operation();
}
