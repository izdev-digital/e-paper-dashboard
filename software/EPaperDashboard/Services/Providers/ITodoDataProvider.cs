using CSharpFunctionalExtensions;

namespace EPaperDashboard.Services.Providers;

/// <summary>
/// Provides todo/task list data for the todo widget.
/// </summary>
public interface ITodoDataProvider
{
    Task<Result<List<TodoItem>, string>> FetchTodoItemsAsync(string dashboardId, string entityId);

    /// <summary>
    /// Discovers all available todo entities and fetches items for each.
    /// </summary>
    Task<Result<Dictionary<string, List<TodoItem>>, string>> FetchAllTodoItemsAsync(string dashboardId);
}
