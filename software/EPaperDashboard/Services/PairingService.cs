using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using System.Security.Cryptography;

namespace EPaperDashboard.Services;

public sealed class PairingService(IPairingSessionRepository pairingSessionRepository)
{
    private const int CodeLength = 6;
    private const int ExpiryMinutes = 5;

    public PairingSession CreatePairingSession(UserId userId)
    {
        var session = new PairingSession
        {
            UserId = userId,
            Code = GenerateCode(),
            ApiKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpiryMinutes),
            IsCompleted = false
        };

        pairingSessionRepository.Insert(session);
        return session;
    }

    public Maybe<PairingSession> GetPairingSessionByCode(string code) =>
        pairingSessionRepository.FindByCode(code);

    public void CompletePairingSession(PairingSessionId sessionId, string deviceIdentifier)
    {
        var session = pairingSessionRepository.FindById(sessionId);
        session.Execute(s =>
        {
            s.IsCompleted = true;
            s.DeviceIdentifier = deviceIdentifier;
            pairingSessionRepository.Update(s);
        });
    }

    public void CleanupExpiredSessions() =>
        pairingSessionRepository.DeleteExpired(DateTimeOffset.UtcNow);

    public bool HasActiveSessions() =>
        pairingSessionRepository.HasActiveSessions(DateTimeOffset.UtcNow);

    private static string GenerateCode()
    {
        var chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var code = new char[CodeLength];

        for (int i = 0; i < CodeLength; i++)
        {
            code[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(code);
    }
}
