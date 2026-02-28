using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using System.Security.Cryptography;

namespace EPaperDashboard.Services;

public sealed class PairingService(IPairingSessionRepository pairingSessionRepository)
{
    private const int CodeLength = 6;
    private const int PinLength = 4;
    private const int ExpiryMinutes = 5;
    public const int MaxFailedAttempts = 5;

    public PairingSession CreatePairingSession(UserId userId)
    {
        var session = new PairingSession
        {
            UserId = userId,
            Code = GenerateCode(),
            ConfirmationPin = GeneratePin(),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpiryMinutes),
            Status = PairingStatus.Pending,
            IsCompleted = false
        };

        pairingSessionRepository.Insert(session);
        return session;
    }

    public Maybe<PairingSession> GetPairingSessionByCode(string code) =>
        pairingSessionRepository.FindByCode(code);

    public Maybe<PairingSession> GetPairingSessionById(PairingSessionId id) =>
        pairingSessionRepository.FindById(id);

    public void SetAwaitingConfirmation(PairingSessionId sessionId, string deviceIdentifier)
    {
        var session = pairingSessionRepository.FindById(sessionId);
        session.Execute(s =>
        {
            s.DeviceIdentifier = deviceIdentifier;
            s.Status = PairingStatus.AwaitingConfirmation;
            pairingSessionRepository.Update(s);
        });
    }

    public void ConfirmPairingSession(PairingSessionId sessionId)
    {
        var session = pairingSessionRepository.FindById(sessionId);
        session.Execute(s =>
        {
            s.Status = PairingStatus.Confirmed;
            s.ApiKey = Guid.NewGuid().ToString("N");
            pairingSessionRepository.Update(s);
        });
    }

    public void CompletePairingSession(PairingSessionId sessionId)
    {
        var session = pairingSessionRepository.FindById(sessionId);
        session.Execute(s =>
        {
            s.IsCompleted = true;
            s.Status = PairingStatus.Completed;
            pairingSessionRepository.Update(s);
        });
    }

    public void IncrementFailedAttempts(PairingSessionId sessionId)
    {
        var session = pairingSessionRepository.FindById(sessionId);
        session.Execute(s =>
        {
            s.FailedAttempts++;
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

    private static string GeneratePin()
    {
        var pin = new char[PinLength];

        for (int i = 0; i < PinLength; i++)
        {
            pin[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(pin);
    }
}
