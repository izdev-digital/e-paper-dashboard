using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbUserLlmConfigRepository(LiteDbContext context) : IUserLlmConfigRepository
{
    public Maybe<UserLlmConfig> FindByUserId(UserId userId) =>
        context.UserLlmConfigs.FindOne(c => c.UserId == userId);

    public void Upsert(UserLlmConfig config)
    {
        if (config.Id == UserLlmConfigId.Empty)
            config.Id = UserLlmConfigId.New();
        context.UserLlmConfigs.Upsert(config);
    }
}
