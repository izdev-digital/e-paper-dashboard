using EPaperDashboard.Controllers;

namespace EPaperDashboard.Services;

public interface IHassRepository
{
    void StoreToken(AccessTokenDto? accessToken);

    AccessTokenDto? RetrieveToken();
}
