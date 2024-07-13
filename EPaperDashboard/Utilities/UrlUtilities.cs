using System.Web;

namespace EPaperDashboard.Utilities;

public static class UriUtilities
{
    public static Uri CreateUri(Uri baseUri, string path, IReadOnlyDictionary<string, string> queryParameters) => new UriBuilder(baseUri)
    {
        Path = path,
        Query = queryParameters.Aggregate(
            HttpUtility.ParseQueryString(string.Empty),
            (seed, item) =>
            {
                seed.Add(item.Key, item.Value);
                return seed;
            }).ToString()
    }.Uri;
}
