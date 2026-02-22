using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.LiteDb;

internal sealed class LiteDbPairingSessionRepository(LiteDbContext context) : IPairingSessionRepository
{
    public Maybe<PairingSession> FindById(Guid id) =>
        context.PairingSessions.FindById(id);

    public Maybe<PairingSession> FindByCode(string code) =>
        context.PairingSessions.FindOne(s => s.Code == code);

    public void Insert(PairingSession session) =>
        context.PairingSessions.Insert(session);

    public void Update(PairingSession session) =>
        context.PairingSessions.Update(session);

    public void DeleteExpired(DateTimeOffset before) =>
        context.PairingSessions.DeleteMany(s => s.ExpiresAt < before);

    public bool HasActiveSessions(DateTimeOffset now) =>
        context.PairingSessions.Exists(s => !s.IsCompleted && s.ExpiresAt > now);
}
