namespace Jubeka.Core.Application.Default;

public sealed class QueryParser(IVariableSubstitutor substitutor) : IQueryParser
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

                string substitutedParam = substitutor.Substitute(value, vars);

                if(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(substitutedParam))
                {
                    continue;
                }

                result.Add((key, substitutedParam));
            }
        }

        return result;
    }
}