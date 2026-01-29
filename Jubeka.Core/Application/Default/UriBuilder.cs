using System.Collections.Specialized;
using System.Web;

namespace Jubeka.Core.Application.Default;

public sealed class UriBuilderHelper : IUriBuilderHelper
{
    public Uri Build(string rawUrl, IReadOnlyList<(string Key, string Value)> queryParams)
    {
        UriBuilder uriBuilder = new (rawUrl);
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach ((string? Key, string? Value) in queryParams)
        {
            query[Key] = Value;
        }

        uriBuilder.Query = query.ToString() ?? string.Empty;
        return uriBuilder.Uri;
    }
}