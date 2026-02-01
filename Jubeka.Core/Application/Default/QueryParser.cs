using System.Collections.Specialized;

namespace Jubeka.Core.Application.Default;

public sealed class QueryParser : IQueryParser
{
    public IReadOnlyList<(string Key, string Value)> Parse(IEnumerable<string>? rawQueryParameters, IReadOnlyDictionary<string, string> vars)
    {
        List<(string Key, string Value)> result = [];

        IEnumerable<string> raw = rawQueryParameters ?? [];

        foreach (string rawParam in raw)
        {
            int separatorIndex = rawParam.IndexOf('=');

            if (separatorIndex >= 1)
            {
                string key = rawParam.Substring(0, separatorIndex);
                string value = rawParam.Substring(separatorIndex + 1);

                string substitutedParam = VariableSubstitutor.Substitute(value, vars);

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(substitutedParam))
                {
                    continue;
                }

                result.Add((key, substitutedParam));
            }
        }

        return result;
    }

    public List<(string Key, string Value)> ParseQueryParametersFromUrl(string rawUrl)
    {
        Uri uri = new(rawUrl);
        NameValueCollection queryCollection = System.Web.HttpUtility.ParseQueryString(uri.Query);
        List<(string Key, string Value)> result = [];

        foreach (string? key in queryCollection.AllKeys)
        {
            if (key == null)
            {
                continue;
            }

            string? value = queryCollection[key];

            if (value == null)
            {
                continue;
            }

            result.Add((key, value));
        }

        return result;
    }
}