using CSharpFunctionalExtensions;
using EPaperDashboard.Models;

namespace EPaperDashboard.Data.Repositories;

public interface IPairingSessionRepository
{
    Maybe<PairingSession> FindById(PairingSessionId id);
    Maybe<PairingSession> FindByCode(string code);
    void Insert(PairingSession session);
    void Update(PairingSession session);
    void DeleteExpired(DateTimeOffset before);
    bool HasActiveSessions(DateTimeOffset now);
}
