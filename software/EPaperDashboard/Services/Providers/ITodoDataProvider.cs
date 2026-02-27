using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides todo/task list data for the todo widget.
/// </summary>
public interface ITodoDataProvider
{
    Task<Result<List<TodoItem>, string>> FetchTodoItemsAsync(string dashboardId, string entityId);
}
