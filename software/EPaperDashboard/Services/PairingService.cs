using CSharpFunctionalExtensions;
using EPaperDashboard.Data;
using EPaperDashboard.Models;
using LiteDB;
using System.Security.Cryptography;

namespace EPaperDashboard.Services;

public sealed class PairingService(LiteDbContext dbContext)
{
    private readonly LiteDbContext _dbContext = dbContext;
    private const int CodeLength = 6;
    private const int ExpiryMinutes = 5;

    public PairingSession CreatePairingSession(ObjectId dashboardId, string apiKey)
    {
        var session = new PairingSession
        {
            DashboardId = dashboardId,
            Code = GenerateCode(),
            ApiKey = apiKey,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpiryMinutes),
            IsCompleted = false
        };
        
        _dbContext.PairingSessions.Insert(session);
        return session;
    }

    public Maybe<PairingSession> GetPairingSessionByCode(string code)
    {
        return _dbContext.PairingSessions.FindOne(s => s.Code == code);
    }

    public void CompletePairingSession(ObjectId sessionId, string deviceIdentifier)
    {
        var session = _dbContext.PairingSessions.FindById(sessionId);
        if (session != null)
        {
            session.IsCompleted = true;
            session.DeviceIdentifier = deviceIdentifier;
            _dbContext.PairingSessions.Update(session);
        }
    }

    public void CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        _dbContext.PairingSessions.DeleteMany(s => s.ExpiresAt < now);
    }

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
