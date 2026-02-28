using CSharpFunctionalExtensions;
using EPaperDashboard.Data.Repositories;
using EPaperDashboard.Models;
using System.Security.Cryptography;

namespace EPaperDashboard.Services;

public sealed class PairingService(IPairingSessionRepository pairingSessionRepository)
{
    private const int CodeLength = 6;
    private const int ExpiryMinutes = 5;
    public const int MaxFailedAttempts = 5;

    public PairingSession CreatePairingSession(UserId userId)
    {
        var session = new PairingSession
        {
            UserId = userId,
            Code = GenerateCode(),
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

    public Maybe<PairingSession> RegisterDevice(string code, string deviceIdentifier, int? screenWidth = null, int? screenHeight = null)
    {
        var session = pairingSessionRepository.FindByCode(code);
        if (session.HasNoValue)
        {
            return Maybe<PairingSession>.None;
        }

        var s = session.Value;
        s.DeviceIdentifier = deviceIdentifier;
        s.ApiKey = Guid.NewGuid().ToString("N");
        s.Status = PairingStatus.Completed;
        s.IsCompleted = true;

        if (screenWidth.HasValue && screenHeight.HasValue)
        {
            s.ScreenWidth = Math.Max(screenWidth.Value, screenHeight.Value);
            s.ScreenHeight = Math.Min(screenWidth.Value, screenHeight.Value);
        }

        pairingSessionRepository.Update(s);
        return s;
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
