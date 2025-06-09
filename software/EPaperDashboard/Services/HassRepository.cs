using System;
using EPaperDashboard.Controllers;

namespace EPaperDashboard.Services;

public class HassRepository : IHassRepository
{
    private AccessTokenDto? _accessToken;

    public AccessTokenDto? RetrieveToken() => _accessToken;

    public void StoreToken(AccessTokenDto? accessToken)
    {
        _accessToken = accessToken;
    }
}
