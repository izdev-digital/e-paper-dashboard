namespace EPaperDashboard.Utilities;

public static class ClientUrlValidator
{
    public static string? GetValidationError(Uri? uri)
    {
        if (uri is null)
        {
            return "CLIENT_URL";
        }

        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "CLIENT_URL must be an absolute HTTP or HTTPS URL";
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port is <= 0 or > 65535)
        {
            return "CLIENT_URL must contain a valid host and port";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "CLIENT_URL must not contain credentials";
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return "CLIENT_URL must not contain a query string or fragment";
        }

        return null;
    }
}
