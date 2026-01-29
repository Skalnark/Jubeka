using System.Collections.Specialized;
using System.Web;

namespace Jubeka.Core.Application.Default;

public sealed class UriBuilderHelper(IVariableSubstitutor variableSubstitutor): IUriBuilderHelper
{
    public Uri Build(string rawUrl, IReadOnlyDictionary<string, string> vars, IReadOnlyList<(string Key, string Value)> queryParams)
    {
        string finalUrl = variableSubstitutor.Substitute(rawUrl, vars);
        UriBuilder uriBuilder = new (finalUrl);
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach ((string? Key, string? Value) in queryParams)
        {
            query[Key] = Value;
        }

        uriBuilder.Query = query.ToString() ?? string.Empty;
        return uriBuilder.Uri;
    }
}