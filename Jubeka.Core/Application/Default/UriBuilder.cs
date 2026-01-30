using Jubeka.Core.Domain;

using System.Collections.Specialized;
using System.Web;

namespace Jubeka.Core.Application.Default;

public sealed class UriBuilderHelper(IQueryParser queryParser): IUriBuilderHelper
{
    public Uri Build(string rawUrl, IReadOnlyDictionary<string, string> vars, IReadOnlyList<(string Key, string Value)> queryParams)
    {
        string finalUrl = VariableSubstitutor.Substitute(rawUrl, vars);
        UriBuilder uriBuilder = new (finalUrl);
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach ((string? Key, string? Value) in queryParams)
        {
            query[Key] = Value;
        }

        uriBuilder.Query = query.ToString() ?? string.Empty;
        return uriBuilder.Uri;
    }

    public Uri BuildFromUrl(string rawUrl, IReadOnlyDictionary<string, string> vars)
    {
        string finalUrl = VariableSubstitutor.Substitute(rawUrl, vars);
        return new Uri(finalUrl);
    }

    public RequestComponents BuildUriComponents(string rawUrl)
    {
        List<(string Key, string Value)> queryParams = queryParser.ParseQueryParametersFromUrl(rawUrl);
        Uri uri = new (rawUrl);

        return new RequestComponents(uri, queryParams);
    }
}