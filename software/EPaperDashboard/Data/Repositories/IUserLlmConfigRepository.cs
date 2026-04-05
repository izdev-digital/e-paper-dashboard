using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IUserLlmConfigRepository
{
    Maybe<UserLlmConfig> FindByUserId(UserId userId);
    void Upsert(UserLlmConfig config);
}
